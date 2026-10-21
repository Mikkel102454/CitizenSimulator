using System.Reflection;
using System.Text.Json;
using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Business.Models;
using CitizenSimulator.Items.Services.Abstractions;
using BusinessDefinition = CitizenSimulator.Business.Models.Business;

namespace CitizenSimulator.Business.Systems;
public class BusinessSystem(IItemService items) : GameSystem
{
    // prob make the recipie and business class parse the json themself
    protected override void OnStart(ICommandBuffer commands)
    {
        Recipe.RecipeList.Clear();
        BusinessDefinition.BusinessList.Clear();

        var itemIds = items.GetItems().Select(item => item.Id).ToHashSet();
        if (itemIds.Count == 0)
            throw new InvalidOperationException("Business definitions require the Items catalog to be loaded first.");

        LoadDefinitions("CitizenSimulator.Business.Recipes.", Recipe.RecipeList, recipe => recipe.Id, "recipe");
        LoadDefinitions("CitizenSimulator.Business.Business.", BusinessDefinition.BusinessList, business => business.Id, "business");
    }

    private static void LoadDefinitions<T>(string prefix, List<T> definitions, Func<T, string> getId, string kind)
    {
        Assembly assembly = typeof(BusinessSystem).Assembly;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var ids = new HashSet<string>();

        foreach (string resourceName in assembly.GetManifestResourceNames().Order())
        {
            if (!resourceName.StartsWith(prefix, StringComparison.Ordinal) || !resourceName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                using Stream stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException("Embedded resource could not be opened.");
                T definition = JsonSerializer.Deserialize<T>(stream, options) ?? throw new JsonException("Definition must not be null.");
                string id = getId(definition);

                if (string.IsNullOrWhiteSpace(id))
                    throw new JsonException("Definition must have a non-empty ID.");

                if (!ids.Add(id))
                    throw new JsonException($"Duplicate {kind} ID '{id}'.");

                definitions.Add(definition);
                Console.WriteLine($"Loaded {kind}: {id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load '{resourceName}': {ex.Message}");
            }
        }
    }

    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {
        
    }
}
