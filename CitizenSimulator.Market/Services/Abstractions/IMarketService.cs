using CitizenSimulator.Business.Components;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Market.Services.Abstractions;

public sealed record MarketOffer(BusinessComponent Seller, string ItemId, decimal UnitPrice, decimal AvailableQuantity);
public sealed record PurchaseResult(IMarketService.BuyStatus Status, decimal Quantity, decimal TotalPrice);
public interface IMarketService
{
    enum BuyStatus
    {
        SUCCESS,
        INSUFFICIENT_FUNDS,
        OUT_OF_STOCK,
        CLOSED_SELLER,
        INVALID_QUANTITY,
        INVENTORY_FULL,
        NO_SERVICE_CAPACITY
    }

    IReadOnlyList<MarketOffer> GetOffers(string itemId, bool wholesale = false);
    BuyStatus TryBuy(InventoryComponent receiveInventory,
        BankAccountComponent paymentAccount,
        string itemId,
        decimal amount);
    PurchaseResult TryBuyUpTo(InventoryComponent receiveInventory,
        BankAccountComponent paymentAccount,
        string itemId,
        decimal amount);
    BuyStatus TryBuyLiquidationStock(BusinessComponent seller,
        InventoryComponent receiveInventory,
        BankAccountComponent paymentAccount,
        string itemId,
        decimal amount,
        decimal unitPrice);
}
