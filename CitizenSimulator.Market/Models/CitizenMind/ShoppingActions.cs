using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.CitizenMind.Configuration;
using CitizenSimulator.CitizenMind.Definitions;
using CitizenSimulator.CitizenMind.Services.Abstractions;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Inventory.Models.CitizenMind;
using CitizenSimulator.Items.Models;
using CitizenSimulator.Items.Services.Abstractions;
using CitizenSimulator.Market.Services.Abstractions;
using CitizenSimulator.Vault.Models.CitizenMind;

namespace CitizenSimulator.Market.Models.CitizenMind;

public static class ShoppingActions
{
    public const string BalanceFact = AccountFacts.Balance;
    public const string FreeCapacityFact = FoodActions.FreeCapacityFact;
    public static string BuyActionId(string itemId) => $"market.buy.{itemId}";
    public static decimal PurchaseQuantity(Item item) => Math.Ceiling(1 / item.QuantityStep) * item.QuantityStep;

    public static void Register(IModContext context)
    {
        var mind = context.Services.Get<ICitizenMindService>();
        var market = context.Services.Get<IMarketService>();
        var items = new List<IReadOnlyDictionary<string, string>>();
        var foods = new List<IReadOnlyDictionary<string, string>>();
        var drinks = new List<IReadOnlyDictionary<string, string>>();
        foreach (var item in context.Services.Get<IItemService>().GetItems().OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            bool consumable = ConsumptionEffects.TryRead(item, out var effects);
            bool isFood = consumable && effects.Hunger < 0;
            bool isDrink = consumable && effects.Thirst < 0;
            if (!isFood && !isDrink)
                mind.RegisterFact(FoodActions.QuantityFact(item.Id),
                    entity => context.Store.GetComponent<InventoryComponent>(entity)?.GetQuantity(item.Id) ?? 0m, canPredict: true);

            mind.RegisterCandidateProvider(new CandidateProviderDescriptor
            {
                Id = $"market.offers.{item.Id}",
                BindingTypes = new Dictionary<string, Type>
                {
                    ["seller"] = typeof(EntityRef),
                    ["unitPrice"] = typeof(decimal),
                    ["quantity"] = typeof(decimal),
                    ["availableQuantity"] = typeof(decimal)
                }
            }, entity => new OfferCandidateProvider(context.Store.GetComponent<CitizenComponent>(entity) is { Alive: true }
                ? market.GetOffers(item.Id) : [], PurchaseQuantity(item)));
            mind.RegisterHandler(BuyActionId(item.Id), new BuyItemHandler(context.Store, market, item.Id));
            IReadOnlyDictionary<string, string> values = new Dictionary<string, string> { ["itemId"] = item.Id };
            items.Add(values);
            if (isFood)
                foods.Add(values);
            if (isDrink)
                drinks.Add(values);
        }
        MindDefinitionLoader.Load(mind, typeof(ShoppingActions).Assembly,
            new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>
            {
                ["items"] = items,
                ["foods"] = foods,
                ["drinks"] = drinks
            });
    }
}
