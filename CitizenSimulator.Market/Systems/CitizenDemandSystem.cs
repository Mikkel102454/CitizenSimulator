using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Services;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.CitizenMind.Components;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Items.Models;
using CitizenSimulator.Items.Services.Abstractions;
using CitizenSimulator.Market.Models.CitizenMind;
using CitizenSimulator.Market.Services.Abstractions;
using CitizenSimulator.Needs.Components;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Market.Systems;

// Observe one real consumption intention per citizen, including unavailable offers.
public sealed class CitizenDemandSystem(IMarketService market, IBusinessService businesses,
    IItemService items, IServiceRegistry? services = null) : GameSystem(priority: -3)
{
    private readonly Dictionary<EntityRef, (InventoryComponent Inventory, string ItemId)> _requests = [];

    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {
        var active = new HashSet<EntityRef>();
        var choices = items.GetConsumableItems().Where(item => item.QuantityStep > 0
            && ConsumptionEffects.TryRead(item, out var effects) && (effects.Hunger < 0 || effects.Thirst < 0))
            .Select(CreateChoice).ToArray();
        foreach (EntityRef entity in Store.Query(MindComponent.GetComponentType()).OrderBy(entity => entity.Id))
        {
            if (Store.GetComponent<MindComponent>(entity) is not { IsActive: true }
                || Store.GetComponent<CitizenComponent>(entity) is not { Alive: true }
                || Store.GetComponent<NeedsComponent>(entity) is not { } needs
                || Store.GetComponent<InventoryComponent>(entity) is not { } inventory
                || Store.GetComponent<BankAccountComponent>(entity) is not { } account)
                continue;

            bool hungry = needs.Hunger >= 0.35 && !choices.Any(choice => choice.HungerRelief > 0 && inventory.GetQuantity(choice.Item.Id) >= 1);
            bool homeWater = services != null && services.TryGet<IHomeAccessService>(out var housing) && housing!.CanDrink(entity);
            bool thirsty = needs.Thirst >= 0.35 && !homeWater
                && !choices.Any(choice => choice.ThirstRelief > 0 && inventory.GetQuantity(choice.Item.Id) >= 1);
            if (!hungry && !thirsty)
                continue;

            bool seekDrink = thirsty && (!hungry || needs.Thirst >= needs.Hunger);
            var eligible = choices.Where(choice => seekDrink ? choice.ThirstRelief > 0 : choice.HungerRelief > 0).ToArray();
            var listed = eligible.Where(choice => choice.Offer != null).ToArray();
            if (listed.Length > 0)
                eligible = listed;

            var affordable = eligible.Where(choice => choice.UnitPrice * ShoppingActions.PurchaseQuantity(choice.Item) <= account.Balance).ToArray();
            if (affordable.Length > 0)
                eligible = affordable;

            var selected = eligible.OrderBy(choice => choice.Offer?.Availability != IMarketService.BuyStatus.SUCCESS)
                .ThenBy(choice => choice.UnitPrice / (seekDrink ? choice.ThirstRelief : choice.HungerRelief))
                .ThenBy(choice => choice.Item.Id, StringComparer.Ordinal).FirstOrDefault();
            if (selected == null)
                continue;

            if (_requests.TryGetValue(entity, out var previous)
                && (previous.ItemId != selected.Item.Id || !ReferenceEquals(previous.Inventory, inventory)))
                market.CancelDemand(previous.Inventory, previous.ItemId);

            market.ReportDemand(inventory, account, selected.Item.Id, ShoppingActions.PurchaseQuantity(selected.Item), selected.Offer?.Seller);
            _requests[entity] = (inventory, selected.Item.Id);
            active.Add(entity);
        }

        foreach (EntityRef entity in _requests.Keys.Where(entity => !active.Contains(entity)).ToArray())
        {
            var previous = _requests[entity];
            market.CancelDemand(previous.Inventory, previous.ItemId);
            _requests.Remove(entity);
        }
    }

    private ConsumptionChoice CreateChoice(Item item)
    {
        var offer = market.GetOffers(item.Id, includeUnavailable: true)
            .OrderBy(offer => offer.Availability != IMarketService.BuyStatus.SUCCESS).ThenBy(offer => offer.UnitPrice).FirstOrDefault();
        decimal fallback = businesses.GetBusinessTypes().Where(type => type.SalesPolicy?.SellItems.ContainsKey(item.Id) == true)
            .Select(type => type.SalesPolicy!.SellItems[item.Id]).DefaultIfEmpty(1).Min();
        ConsumptionEffects.TryRead(item, out var effects);
        return new ConsumptionChoice(item, offer, offer?.UnitPrice ?? fallback,
            (decimal)-effects.Hunger, (decimal)-effects.Thirst);
    }

    private sealed record ConsumptionChoice(Item Item, MarketOffer? Offer, decimal UnitPrice, decimal HungerRelief, decimal ThirstRelief);
}