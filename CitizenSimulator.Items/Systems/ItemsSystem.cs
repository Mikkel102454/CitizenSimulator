using System.Reflection;
using System.Text.Json;
using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Items.Models;

namespace CitizenSimulator.Items.Systems;

public class ItemsSystem(IEventBus events) : GameSystem
{
    // prob make the item and effect class parse the json themself
    protected override void OnStart(ICommandBuffer commands)
    {
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