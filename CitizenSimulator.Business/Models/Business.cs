namespace CitizenSimulator.Business.Models;

public class Business
{
    internal static List<Business> BusinessList { get; } = [];
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public List<string> Recipes { get; init; } = [];
    public PremisesRequirements Premises { get; init; } = new();
    public List<WorkforceRequirement> Workforce { get; init; } = [];
    public List<EquipmentRequirement> Equipment { get; init; } = [];
    public List<OpeningHours> OpeningHours { get; init; } = [];
    public OperatingUtilities Utilities { get; init; } = new();
    public OperatingCosts OperatingCosts { get; init; } = new();
    public List<StockPolicy> StockPolicy { get; init; } = [];
    public SalesPolicy? SalesPolicy { get; init; }
    public DisposalPolicy? DisposalPolicy { get; init; }
    public GridSupplyPolicy? GridSupplyPolicy { get; init; }
    public decimal MinimumCapital { get; init; }
    public decimal BankruptcyGraceDays { get; init; } = 7;
    public Dictionary<string, decimal> ProductionTargets { get; init; } = [];
}
