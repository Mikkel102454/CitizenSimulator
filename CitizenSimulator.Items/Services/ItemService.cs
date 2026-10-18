using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Items.Models;
using CitizenSimulator.Items.Services.Abstractions;

namespace CitizenSimulator.Items.Services;

public class ItemService(IEntityStore store, IEventBus events) : IItemService
{
    public Item[] GetItems()
    {
        return Item.ItemList.ToArray();
    }

    public Item[] GetConsumableItems()
    {
        return Item.ItemList.Where(item => item.Consumable).ToArray();
    }
}