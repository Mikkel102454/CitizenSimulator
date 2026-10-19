using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Market.Services.Abstractions;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Market.Services;

public class MarketService(IEntityStore store, IEventBus events) : IMarketService
{
    public bool TryBuy(InventoryComponent receiveInventory, VaultComponent.Account paymentAccount, string itemId, int amount)
    {
        foreach (var seller in store.Query(BusinessComponent.GetComponentType()))
        {
            var business = store.GetComponent<BusinessComponent>(seller);
            
            if (!business.IsOperating)
                continue;
            
            var salesPolicy = business.Type.SalesPolicy;
            
            if (salesPolicy is null || !salesPolicy.SellItems.Contains(itemId))
                continue;
            
            var inventory = store.GetComponent<InventoryComponent>(seller);
            
            if (inventory.GetQuantity(itemId) < amount)
                continue;

            
        }

        return false;
    }
}