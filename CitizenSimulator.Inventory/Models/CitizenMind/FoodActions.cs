using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Services.Abstractions;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Inventory.Services.Abstractions;
using CitizenSimulator.Items.Models;
using CitizenSimulator.Items.Services.Abstractions;

namespace CitizenSimulator.Inventory.Models.CitizenMind;

/// <summary>Grounded food actions and stock facts without an Items -> Inventory dependency.</summary>
public static class FoodActions
{
    public const double EatingMinutes = 5;
    public const string FreeCapacityFact = "inventory.freeCapacity";
    public static string QuantityFact(string itemId)
    {
        return $"inventory.quantity.{itemId}";
    }

    public static void Register(IModContext context)
    {
        var mind = context.Services.Get<ICitizenMindService>();
        var inventoryService = context.Services.Get<IInventoryService>();
        var actions = new List<string>();
        mind.RegisterFact(FreeCapacityFact,
            entity => context.Store.GetComponent<InventoryComponent>(entity)?.FreeCapacity ?? 0m,
            canPredict: true);
        foreach (var item in context.Services.Get<IItemService>().GetConsumableItems()
            .OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            if (!ConsumptionEffects.TryRead(item, out var effects) || effects.Hunger >= 0)
                continue;

            var action = CreateAction(item, effects);
            mind.RegisterFact(
                QuantityFact(item.Id),
                entity => context.Store.GetComponent<InventoryComponent>(entity)?.GetQuantity(item.Id) ?? 0m,
                canPredict: true);
            mind.RegisterHandler(action.Handler, new EatItemHandler(context.Store, inventoryService, item));
            mind.RegisterAction(action);
            actions.Add(action.Id);
        }

        if (actions.Count == 0)
            return;

        mind.RegisterGoal(new GoalDefinition
        {
            Id = "citizen_brain.core.satisfy_hunger",
            Eligible = Op("gte", Fact("needs.hunger"), Literal(0.35)),
            Emergency = new()
            {
                When = Op("gte", Fact("needs.hunger"), Literal(0.90)),
                Priority = 90
            },
            Desired = [Op("lte", Fact("needs.hunger"), Literal(0.25))],
            Utility = new()
            {
                Considerations =
                [
                    new()
                    {
                        Input = Fact("needs.hunger"),
                        Curve = [[0, 0], [0.35, 0.15], [0.70, 0.75], [1, 1]]
                    }
                ]
            },
            GoalActions = actions
        });
    }

    public static ActionDefinition CreateAction(Item item, ConsumptionEffects effects)
    {
        string stock = QuantityFact(item.Id);
        var prediction = new List<PredictionEffectDefinition>
        {
            new()
            {
                Fact = stock,
                Op = "subtract",
                Value = Literal(1m)
            },
            new()
            {
                Fact = FreeCapacityFact,
                Op = "add",
                Value = Op("min", Literal(1m), Op("subtract", Literal(decimal.MaxValue), Fact(FreeCapacityFact)))
            }
        };
        void PredictNeed(string fact, double change)
        {
            if (change == 0)
                return;

            var changedNeed = Op("add", Fact(fact), Literal(change));
            var nonNegativeNeed = Op("max", Literal(0.0), changedNeed);
            var boundedNeed = Op("min", Literal(1.0), nonNegativeNeed);
            prediction.Add(new()
            {
                Fact = fact,
                Op = "set",
                Value = boundedNeed
            });
        }

        PredictNeed("needs.hunger", effects.Hunger);
        PredictNeed("needs.thirst", effects.Thirst);
        PredictNeed("needs.energy", effects.Energy);
        return new()
        {
            Id = $"inventory.eat.{item.Id}",
            Handler = $"inventory.eat.{item.Id}",
            Preconditions = [Op("gte", Fact(stock), Literal(1m)), Op("gt", Fact("needs.hunger"), Literal(0.25))],
            Duration = new()
            {
                Unit = "simulationMinutes",
                Estimate = Literal(EatingMinutes)
            },
            Prediction = prediction,
            // Completing a portion does not necessarily satisfy the hunger goal.
            Completion = [Op("lte", Fact(stock), Op("subtract", new() { Before = stock }, Literal(1m)))],
            Cost = new()
            {
                Expression = new()
                {
                    Calculated = "durationMinutes"
                }
            },
            Execution = new()
            {
                Interruptible = true,
                TimeoutMinutes = 15
            }
        };
    }

    private static ExpressionDefinition Fact(string fact)
    {
        return new()
        {
            Fact = fact
        };
    }

    private static ExpressionDefinition Literal(object value)
    {
        return new()
        {
            Literal = value
        };
    }

    private static ExpressionDefinition Op(string op, ExpressionDefinition left, ExpressionDefinition right)
    {
        return new()
        {
            Op = op,
            Args = [left, right]
        };
    }
}
