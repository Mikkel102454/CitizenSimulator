using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Inventory.Models;
using CitizenSimulator.Items.Models;

namespace CitizenSimulator.Inventory.Services.Abstractions;

public interface IInventoryService
{
    bool TryAdd(string itemId, InventoryComponent inventory, decimal amount, decimal totalValue = 0);
    bool TryRemove(string itemId, InventoryComponent inventory, decimal amount, out decimal removedValue);
    bool TryTransfer(string itemId,
        InventoryComponent sender,
        InventoryComponent receiver,
        decimal amount,
        decimal receiverValue,
        out decimal removedValue);
    bool TryReserve(InventoryComponent inventory,
        IReadOnlyDictionary<string, decimal> quantities,
        out InventoryReservation? reservation);
    bool TryRelease(InventoryComponent inventory, InventoryReservation reservation);
    bool TryExchange(InventoryComponent inventory,
        InventoryReservation reservation,
        IReadOnlyDictionary<string, decimal> outputs,
        IReadOnlyDictionary<string, decimal> outputValues);
    bool TryConsume(Item item, InventoryComponent inventory);
}
