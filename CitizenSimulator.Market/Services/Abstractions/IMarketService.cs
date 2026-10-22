using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Market.Services.Abstractions;

public interface IMarketService
{
    enum BuyStatus
    {
        SUCCESS,
        INSUFFICIENT_FUNDS,
        OUT_OF_STOCK,
        CLOSED_SELLER,
        INVALID_QUANTITY
    }
    BuyStatus TryBuy(InventoryComponent receiveInventory, BankAccountComponent paymentAccount, String itemId, int amount);
}