using CitizenSimulator.Items.Models;

namespace CitizenSimulator.Items.Services.Abstractions;

public interface IItemService
{
    Item[] GetItems();
    Item[] GetConsumableItems();
    
}