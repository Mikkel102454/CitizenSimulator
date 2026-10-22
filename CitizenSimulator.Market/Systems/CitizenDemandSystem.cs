using CitizenSimulator.Abstractions.ECS;
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

// Observe real needs, including when no purchasable offer exists. Planning itself stays read-only.
public sealed class CitizenDemandSystem(IMarketService market, IBusinessService businesses,
    IItemService items) : GameSystem(priority: -3)
{
    private readonly Dictionary<EntityRef, (InventoryComponent Inventory, string ItemId)> _foodRequests = [];

    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {
        var activeRequests = new HashSet<EntityRef>();
        var foods = items.GetConsumableItems()
            .Where(item => item.QuantityStep > 0 && ConsumptionEffects.TryRead(item, out var effects) && effects.Hunger < 0)
            .ToArray();
        var foodChoices = foods.Select(CreateFoodChoice).ToArray();
        foreach (var entity in Store.Query(MindComponent.GetComponentType()).OrderBy(entity => entity.Id))
        {
            if (Store.GetComponent<MindComponent>(entity) is not { IsActive: true }
                || Store.GetComponent<CitizenComponent>(entity) is not { Alive: true }
                || Store.GetComponent<NeedsComponent>(entity) is not { Hunger: >= 0.35 }
                || Store.GetComponent<InventoryComponent>(entity) is not { } inventory
                || Store.GetComponent<BankAccountComponent>(entity) is not { } account
                || foods.Any(food => inventory.GetQuantity(food.Id) >= 1))
                continue;

            var choices = foodChoices;
            var listedChoices = choices.Where(choice => choice.Offer != null).ToArray();
            if (listedChoices.Length > 0)
                choices = listedChoices;

            var affordable = choices.Where(choice => choice.UnitPrice * ShoppingActions.PurchaseQuantity(choice.Item)
                <= account.Balance).ToArray();
            if (affordable.Length > 0)
                choices = affordable;

            var selected = choices
                .OrderBy(choice => choice.Offer?.Availability != IMarketService.BuyStatus.SUCCESS)
                .ThenBy(choice => choice.UnitPrice / choice.HungerRelief)
                .ThenBy(choice => choice.Item.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (selected == null)
                continue;

            if (_foodRequests.TryGetValue(entity, out var previous)
                && (previous.ItemId != selected.Item.Id || !ReferenceEquals(previous.Inventory, inventory)))
                market.CancelDemand(previous.Inventory, previous.ItemId);

            market.ReportDemand(inventory, account, selected.Item.Id,
                ShoppingActions.PurchaseQuantity(selected.Item), selected.Offer?.Seller);
            _foodRequests[entity] = (inventory, selected.Item.Id);
            activeRequests.Add(entity);
        }

        foreach (var entity in _foodRequests.Keys.Where(entity => !activeRequests.Contains(entity)).ToArray())
        {
            var previous = _foodRequests[entity];
            market.CancelDemand(previous.Inventory, previous.ItemId);
            _foodRequests.Remove(entity);
        }
    }

    private FoodChoice CreateFoodChoice(Item item)
    {
        var offer = market.GetOffers(item.Id, includeUnavailable: true)
            .OrderBy(offer => offer.Availability != IMarketService.BuyStatus.SUCCESS)
            .ThenBy(offer => offer.UnitPrice)
            .FirstOrDefault();
        decimal fallbackPrice = businesses.GetBusinessTypes()
            .Where(type => type.SalesPolicy?.SellItems.ContainsKey(item.Id) == true)
            .Select(type => type.SalesPolicy!.SellItems[item.Id])
            .DefaultIfEmpty(1).Min();
        ConsumptionEffects.TryRead(item, out var effects);
        return new FoodChoice(item, offer, offer?.UnitPrice ?? fallbackPrice, (decimal)-effects.Hunger);
    }

    private sealed record FoodChoice(Item Item, MarketOffer? Offer, decimal UnitPrice, decimal HungerRelief);
}
