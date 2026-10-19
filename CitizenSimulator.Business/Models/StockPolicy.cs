namespace CitizenSimulator.Business.Models;

public class StockPolicy
{
    public string ItemId { get; init; } = "";
    public decimal ReorderBelow { get; init; }
    public decimal TargetQuantity { get; init; }
}
