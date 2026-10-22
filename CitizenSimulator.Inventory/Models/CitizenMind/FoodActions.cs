using System.Globalization;
using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.CitizenMind.Configuration;
using CitizenSimulator.CitizenMind.Services.Abstractions;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Inventory.Services.Abstractions;
using CitizenSimulator.Items.Models;
using CitizenSimulator.Items.Services.Abstractions;

namespace CitizenSimulator.Inventory.Models.CitizenMind;

/// <summary>Registers inventory observations, catalog data and authoritative consumption handlers.</summary>
public static class FoodActions
{
    public const string FreeCapacityFact = "inventory.freeCapacity";
    public static string QuantityFact(string itemId) => $"inventory.quantity.{itemId}";

    public static void Register(IModContext context)
    {
        var mind = context.Services.Get<ICitizenMindService>();
        var inventories = context.Services.Get<IInventoryService>();
        var foods = new List<IReadOnlyDictionary<string, string>>();
        var drinks = new List<IReadOnlyDictionary<string, string>>();
        mind.RegisterFact(FreeCapacityFact,
            entity => context.Store.GetComponent<InventoryComponent>(entity)?.FreeCapacity ?? 0m, canPredict: true);
        foreach (var item in context.Services.Get<IItemService>().GetConsumableItems().OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            if (!ConsumptionEffects.TryRead(item, out var effects) || (effects.Hunger >= 0 && effects.Thirst >= 0))
                continue;
            mind.RegisterFact(QuantityFact(item.Id),
                entity => context.Store.GetComponent<InventoryComponent>(entity)?.GetQuantity(item.Id) ?? 0m, canPredict: true);
            IReadOnlyDictionary<string, string> values = new Dictionary<string, string>
            {
                ["itemId"] = item.Id,
                ["hungerChange"] = effects.Hunger.ToString("R", CultureInfo.InvariantCulture),
                ["thirstChange"] = effects.Thirst.ToString("R", CultureInfo.InvariantCulture),
                ["energyChange"] = effects.Energy.ToString("R", CultureInfo.InvariantCulture)
            };
            if (effects.Hunger < 0)
            {
                mind.RegisterHandler($"inventory.eat.{item.Id}", new EatItemHandler(context.Store, inventories, item));
                foods.Add(values);
            }
            if (effects.Thirst < 0)
            {
                mind.RegisterHandler($"inventory.drink.{item.Id}", new EatItemHandler(context.Store, inventories, item));
                drinks.Add(values);
            }
        }
        MindDefinitionLoader.Load(mind, typeof(FoodActions).Assembly,
            new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>
            {
                ["foods"] = foods,
                ["drinks"] = drinks
            });
    }
}
