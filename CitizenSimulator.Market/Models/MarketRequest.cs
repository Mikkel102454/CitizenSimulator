using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Market.Services.Abstractions;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Market.Models;

internal sealed class MarketRequest
{
    public required InventoryComponent Buyer { get; init; }
    public required string ItemId { get; init; }
    public required BankAccountComponent PaymentAccount { get; init; }
    public EntityRef? Seller { get; set; }
    public decimal RemainingQuantity { get; set; }
    public IMarketService.BuyStatus Status { get; set; }
    public TimeSpan LastObservedAt { get; set; }
    public decimal LargestFailedQuantity { get; set; }
}
