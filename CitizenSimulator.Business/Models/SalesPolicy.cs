namespace CitizenSimulator.Business.Models;

public class SalesPolicy
{
    public List<string> Channels { get; init; } = [];
    public List<string> SellItems { get; init; } = [];
    public decimal TargetMarkupPercent { get; init; }
    public decimal CustomersServedPerWorkerHour { get; init; }
}
