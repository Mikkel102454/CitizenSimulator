using CitizenSimulator.Market.Services.Abstractions;

namespace CitizenSimulator.Market.Models;

// Quantities and revenue cover a rolling game day. Stock is sampled hourly.
public sealed record MarketStatistics(TimeSpan WindowStart, TimeSpan WindowEnd, decimal SoldQuantity, decimal Revenue, decimal PendingQuantity, decimal UnmetQuantity,
    decimal ShortageQuantity, decimal StockLevel, decimal AverageStock)
{
    public decimal DemandQuantity => SoldQuantity + PendingQuantity;
    public decimal? InventoryTurnover => AverageStock > 0 ? SoldQuantity / AverageStock : null;
    public IReadOnlyDictionary<IMarketService.BuyStatus, decimal> Failures { get; init; }
        = new Dictionary<IMarketService.BuyStatus, decimal>();
}
