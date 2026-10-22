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
    public IMarketService.BuyStatus TryBuy(InventoryComponent receiveInventory, BankAccountComponent paymentAccount, string itemId, int amount)
    {
        if(amount <= 0)
            return IMarketService.BuyStatus.INVALID_QUANTITY;
        
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
            
            if(!vaultService.TransferBalance(paymentAccount, business.PaymentAccount, price * amount)) 
                continue;

            if (receiveInventory.Inventory.ContainsKey(itemId))
            {
                receiveInventory.Inventory[itemId] += amount;
            }
            else
            {
                receiveInventory.Inventory.Add(itemId, amount);
            }
            businessInventory.Inventory[itemId] -= amount;
            
            return IMarketService.BuyStatus.SUCCESS;
        }

        return IMarketService.BuyStatus.OUT_OF_STOCK;
    }
}
