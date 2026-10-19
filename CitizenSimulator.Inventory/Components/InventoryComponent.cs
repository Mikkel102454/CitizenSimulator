using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Items.Models;

namespace CitizenSimulator.Inventory.Components;

public class InventoryComponent : Component<InventoryComponent>
{
    public Dictionary<string, decimal> Inventory { get; set; } = [];

    public decimal GetQuantity(Item item) => GetQuantity(item.Id);

    public decimal GetQuantity(string itemId)
    {
        Inventory.TryGetValue(itemId, out decimal quantity);
        return quantity;
    }
}