using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Market.Services.Abstractions;

public interface IMarketService
{
    bool TryBuy(InventoryComponent receiveInventory, VaultComponent.Account paymentAccount, String itemId, int amount);
}