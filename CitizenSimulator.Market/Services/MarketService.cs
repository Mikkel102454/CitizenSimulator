using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Market.Services.Abstractions;
using CitizenSimulator.Vault.Components;
using CitizenSimulator.Vault.Services.Abstractions;

namespace CitizenSimulator.Market.Services;

public class MarketService(IEntityStore store, IEventBus events, IVaultService vaultService) : IMarketService
{
    public bool TryBuy(InventoryComponent receiveInventory, VaultComponent.Account paymentAccount, string itemId, int amount)
    {
        foreach (var seller in store.Query(BusinessComponent.GetComponentType()))
        {
            var business = store.GetComponent<BusinessComponent>(seller);
            
            if (!business.IsOperating)
                continue;
            
            var salesPolicy = business.Type.SalesPolicy;
            
            if (salesPolicy is null || !salesPolicy.SellItems.ContainsKey(itemId))
                continue;
            
            var businessInventory = store.GetComponent<InventoryComponent>(seller);
            
            if (businessInventory.GetQuantity(itemId) < amount)
                continue;

            if(!salesPolicy.SellItems.TryGetValue(itemId, out var price))
                continue;
            
            vaultService.TransferBalance(paymentAccount, business.PaymentAccount, price * amount);
            
            receiveInventory.Inventory[itemId] += amount;
            businessInventory.Inventory[itemId] -= amount;
            
            return true;
        }

        return false;
    }
}
