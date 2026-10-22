using CitizenSimulator.Business.Components;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Vault.Components;
using CitizenSimulator.Market.Models;

namespace CitizenSimulator.Market.Services.Abstractions;

public sealed record MarketOffer(
    BusinessComponent Seller,
    string ItemId,
    decimal UnitPrice,
    decimal AvailableQuantity,
    bool Wholesale = false,
    IMarketService.BuyStatus Availability = IMarketService.BuyStatus.SUCCESS,
    TimeSpan? AvailableFrom = null,
    TimeSpan? AvailableUntil = null);
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
        NO_SERVICE_CAPACITY,
        PRICE_CHANGED,
        ACCESS_DENIED,
        INVALID_BUYER
    }

    IReadOnlyList<MarketOffer> GetOffers(string itemId, bool wholesale = false, bool includeUnavailable = false);
    BuyStatus CheckOffer(MarketOffer offer, InventoryComponent receiveInventory,
        BankAccountComponent paymentAccount, decimal amount);
    BuyStatus TryBuyOffer(MarketOffer offer, InventoryComponent receiveInventory,
        BankAccountComponent paymentAccount, decimal amount);
    void ReportDemand(InventoryComponent receiveInventory, BankAccountComponent paymentAccount,
        string itemId, decimal amount, BusinessComponent? preferredSeller = null, BuyStatus? failureStatus = null);
    void CancelDemand(InventoryComponent receiveInventory, string itemId);
    MarketStatistics GetStatistics(string itemId, BusinessComponent? seller = null);
    decimal GetRestockTarget(BusinessComponent business, string itemId, decimal configuredTarget);
    void AdvanceMarket(TimeSpan gameTime);
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
