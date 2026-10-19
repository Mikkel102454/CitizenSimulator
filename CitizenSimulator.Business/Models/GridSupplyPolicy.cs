namespace CitizenSimulator.Business.Models;

public class GridSupplyPolicy
{
    public string Utility { get; init; } = "";
    public bool ConnectionRequired { get; init; }
    public decimal MaxExportKw { get; init; }
}
