using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Inventory.Services.Abstractions;
using CitizenSimulator.Items.Models;
using CitizenSimulator.Items.Services.Abstractions;

namespace CitizenSimulator.Inventory.Services;

public class InventoryService(IEntityStore store, IEventBus events, IConsumptionService consumptionService) : IInventoryService
{
    public bool TryConsume(Item item, InventoryComponent inventory)
    {
        if(!inventory.Inventory.TryGetValue(item.Id, out var inventoryItem))
            return false;
        
        if(inventoryItem < 1)
            return false;

        if(!consumptionService.ConsumeItem(item, store.GetComponent<CitizenComponent>(inventory.Parent)))
            return false;
        
        inventory.Inventory[item.Id] -= 1;
        return true;
    }
}