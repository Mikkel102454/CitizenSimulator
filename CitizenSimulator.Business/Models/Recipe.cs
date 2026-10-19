namespace CitizenSimulator.Business.Models;

public class Recipe
{
    internal static List<Recipe> RecipeList { get; } = [];

    public string Id { get; init; } = "";
    public Dictionary<string, decimal> Inputs { get; init; } = [];
    public Dictionary<string, decimal> Outputs { get; init; } = [];
    public decimal DurationMinutes { get; init; }
    public decimal LaborMinutes { get; init; }
    public List<EquipmentRequirement> RequiredEquipment { get; init; } = [];
    public BatchUtilities UtilitiesPerBatch { get; init; } = new();
    public BatchUtilities UtilityOutputsPerBatch { get; init; } = new();
    public Dictionary<string, decimal> WasteOutputs { get; init; } = [];
}
