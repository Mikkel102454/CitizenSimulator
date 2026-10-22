using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Market.Models;

internal sealed record MarketSale(TimeSpan Time, EntityRef Seller, string ItemId, decimal Quantity, decimal Revenue);
