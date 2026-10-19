namespace CitizenSimulator.Business.Models;

public class SalesPolicy
{
    public List<string> Channels { get; init; } = [];
    // Item ID mapped to its selling price per inventory unit.
    public Dictionary<string, decimal> SellItems { get; init; } = [];
    public decimal TargetMarkupPercent { get; init; }
    public decimal CustomersServedPerWorkerHour { get; init; }
}
