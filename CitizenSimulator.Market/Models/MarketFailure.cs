using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Market.Services.Abstractions;

namespace CitizenSimulator.Market.Models;

internal sealed record MarketFailure(TimeSpan Time, EntityRef? Seller, string ItemId,
    decimal Quantity, IMarketService.BuyStatus Status);
