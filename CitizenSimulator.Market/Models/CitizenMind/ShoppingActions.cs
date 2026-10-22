using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.CitizenMind.Definitions;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Services.Abstractions;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Inventory.Models.CitizenMind;
using CitizenSimulator.Items.Models;
using CitizenSimulator.Items.Services.Abstractions;
using CitizenSimulator.Market.Services.Abstractions;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Market.Models.CitizenMind;

public static class ShoppingActions
{
    public const string BalanceFact = "market.balance";
    public const string FreeCapacityFact = FoodActions.FreeCapacityFact;
    public const double PurchaseMinutes = 5;

    public static string BuyActionId(string itemId)
    {
        return $"market.buy.{itemId}";
    }

    public static decimal PurchaseQuantity(Item item)
    {
        return Math.Ceiling(1 / item.QuantityStep) * item.QuantityStep;
    }

    public static void Register(IModContext context)
    {
        var mind = context.Services.Get<ICitizenMindService>();
        var market = context.Services.Get<IMarketService>();
        var foodActions = new List<string>();
        mind.RegisterFact(BalanceFact, entity => context.Store.GetComponent<BankAccountComponent>(entity)?.Balance ?? 0m,
            canPredict: true);

        foreach (var item in context.Services.Get<IItemService>().GetItems().OrderBy(item => item.Id))
        {
            bool isFood = ConsumptionEffects.TryRead(item, out var effects) && effects.Hunger < 0;
            string quantityFact = FoodActions.QuantityFact(item.Id);
            // Inventory already registers stock facts and eating actions for hunger-reducing foods.
            if (!isFood)
            {
                mind.RegisterFact(quantityFact,
                    entity => context.Store.GetComponent<InventoryComponent>(entity)?.GetQuantity(item.Id) ?? 0m,
                    canPredict: true);
            }

            string actionId = BuyActionId(item.Id);
            string providerId = $"market.offers.{item.Id}";
            mind.RegisterCandidateProvider(new CandidateProviderDescriptor
            {
                Id = providerId,
                BindingTypes = new Dictionary<string, Type>
                {
                    ["seller"] = typeof(EntityRef),
                    ["unitPrice"] = typeof(decimal),
                    ["quantity"] = typeof(decimal),
                    ["availableQuantity"] = typeof(decimal)
                }
            }, entity => new OfferCandidateProvider(
                context.Store.GetComponent<CitizenComponent>(entity) is { Alive: true }
                    ? market.GetOffers(item.Id) : [], PurchaseQuantity(item)));
            mind.RegisterHandler(actionId, new BuyItemHandler(context.Store, market, item.Id));
            mind.RegisterAction(CreateAction(item.Id, providerId));
            if (isFood)
                foodActions.Add(actionId);
        }

        if (foodActions.Count > 0)
            mind.AddGoalActions("citizen_brain.core.satisfy_hunger", foodActions);
    }

    private static ActionDefinition CreateAction(string itemId, string providerId)
    {
        var quantity = new ExpressionDefinition { Binding = "quantity" };
        var price = new ExpressionDefinition { Binding = "unitPrice" };
        var total = Op("multiply", quantity, price);
        string stock = FoodActions.QuantityFact(itemId);
        return new ActionDefinition
        {
            Id = BuyActionId(itemId),
            Handler = BuyActionId(itemId),
            CandidateProvider = providerId,
            Preconditions =
            [
                Op("gte", new ExpressionDefinition { Fact = BalanceFact }, total),
                Op("gte", new ExpressionDefinition { Fact = FreeCapacityFact }, quantity)
            ],
            Duration = new DurationDefinition
            {
                Unit = "simulationMinutes",
                Estimate = new ExpressionDefinition { Literal = PurchaseMinutes }
            },
            Prediction =
            [
                new() { Fact = stock, Op = "add", Value = quantity },
                new() { Fact = BalanceFact, Op = "subtract", Value = total },
                new() { Fact = FreeCapacityFact, Op = "subtract", Value = quantity }
            ],
            Completion =
            [
                Op("gte", new ExpressionDefinition { Fact = stock },
                    Op("add", new ExpressionDefinition { Before = stock }, quantity))
            ],
            Cost = new CostDefinition
            {
                Expression = Op("add", total, new ExpressionDefinition { Literal = PurchaseMinutes })
            },
            Execution = new ExecutionDefinition { Interruptible = true, TimeoutMinutes = 30 }
        };
    }

    private static ExpressionDefinition Op(string operation, ExpressionDefinition left, ExpressionDefinition right)
    {
        return new ExpressionDefinition { Op = operation, Args = [left, right] };
    }
}
