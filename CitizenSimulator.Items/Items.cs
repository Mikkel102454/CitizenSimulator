using System.Reflection;
using System.Text.Json;
using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Items.Models;
using CitizenSimulator.Items.Services;
using CitizenSimulator.Items.Services.Abstractions;
using CitizenSimulator.Items.Systems;
using CitizenSimulator.Needs.Services;
using CitizenSimulator.Needs.Services.Abstractions;

namespace CitizenSimulator.Items;

[Mod("items", "Items", "1.0.0", Dependencies = new[] { "citizen", "needs" })]
public class Items : IMod
{
    public void OnLoad(IModContext context)
    {
        // Dependent mods register item-specific facts/actions before mind validation.
        LoadCatalog();
        context.Services.Register<IConsumptionService>(new ConsumptionService(context.Store, context.Events, context.Services.Get<INeedsService>()));
        context.Services.Register<IItemService>(new ItemService(context.Store, context.Events));
    }
    
    internal static void LoadCatalog()
    {
        Item.ItemList.Clear();
        Assembly assembly = typeof(ItemsSystem).Assembly;

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        foreach (string resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.EndsWith(".json",
                    StringComparison.OrdinalIgnoreCase))
                continue;

            if (!resourceName.StartsWith("CitizenSimulator.Items.Items.", StringComparison.Ordinal))
                continue;

            try
            {
                using Stream? stream = assembly.GetManifestResourceStream(resourceName);

                if (stream is null)
                    continue;

                Item? item = JsonSerializer.Deserialize<Item>(stream, options);

                if (item is null)
                    continue;

                Item.ItemList.Add(item);

                Console.WriteLine($"Loaded item: {item.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Failed to load '{resourceName}': {ex.Message}"
                );
            }
        }
    }
}



