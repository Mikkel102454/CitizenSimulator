using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Market.Models;

internal sealed record StockSample(TimeSpan Time, EntityRef Seller, string ItemId, decimal Quantity);
