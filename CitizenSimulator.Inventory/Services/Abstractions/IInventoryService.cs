using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Items.Models;

namespace CitizenSimulator.Inventory.Services.Abstractions;

public interface IInventoryService
{
    public bool TryConsume(Item item, InventoryComponent inventory);
}