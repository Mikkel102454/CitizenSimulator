using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Inventory.Models;
using CitizenSimulator.Inventory.Services.Abstractions;
using CitizenSimulator.Items.Models;
using CitizenSimulator.Items.Services.Abstractions;

namespace CitizenSimulator.Inventory.Services;

public class InventoryService(IEntityStore store,
    IEventBus events,
    IConsumptionService consumptionService,
    IItemService items) : IInventoryService
{
    private bool IsValidQuantity(string itemId, decimal quantity)
    {
        return quantity > 0
            && items.GetItems().FirstOrDefault(item => item.Id == itemId) is { QuantityStep: > 0 } item
            && quantity % item.QuantityStep == 0;
    }

    public bool TryAdd(string itemId, InventoryComponent inventory, decimal amount, decimal totalValue = 0)
    {
        if (string.IsNullOrWhiteSpace(itemId) || !IsValidQuantity(itemId, amount) || totalValue < 0
            || inventory.Inventory.Values.Any(quantity => quantity < 0) || inventory.Capacity < 0
            || amount > inventory.FreeCapacity)
            return false;

        decimal quantityAfter = inventory.GetQuantity(itemId) + amount;
        decimal valueAfter = inventory.GetValue(itemId) + totalValue;
        inventory.Inventory[itemId] = quantityAfter;
        inventory.Values[itemId] = valueAfter;
        return true;
    }

    public bool TryRemove(string itemId, InventoryComponent inventory, decimal amount, out decimal removedValue)
    {
        removedValue = 0;
        decimal quantity = inventory.GetQuantity(itemId);
        if (!IsValidQuantity(itemId, amount) || quantity < amount)
            return false;

        decimal value = inventory.GetValue(itemId);
        removedValue = amount == quantity ? value : value * (amount / quantity);
        inventory.Inventory[itemId] = quantity - amount;
        inventory.Values[itemId] = value - removedValue;
        return true;
    }

    public bool TryTransfer(
        string itemId,
        InventoryComponent sender,
        InventoryComponent receiver,
        decimal amount,
        decimal receiverValue,
        out decimal removedValue)
    {
        removedValue = 0;
        decimal quantity = sender.GetQuantity(itemId);
        if (ReferenceEquals(sender, receiver)
            || string.IsNullOrWhiteSpace(itemId)
            || !IsValidQuantity(itemId, amount)
            || quantity < amount
            || receiverValue < 0
            || receiver.Capacity < 0
            || amount > receiver.FreeCapacity)
            return false;

        decimal senderValue = sender.GetValue(itemId);
        decimal cost = amount == quantity ? senderValue : senderValue * (amount / quantity);
        decimal receiverQuantity = receiver.GetQuantity(itemId) + amount;
        decimal valueAfter = receiver.GetValue(itemId) + receiverValue;

        sender.Inventory[itemId] = quantity - amount;
        sender.Values[itemId] = senderValue - cost;
        receiver.Inventory[itemId] = receiverQuantity;
        receiver.Values[itemId] = valueAfter;
        removedValue = cost;
        return true;
    }

    public bool TryReserve(InventoryComponent inventory, IReadOnlyDictionary<string, decimal> quantities, out InventoryReservation? reservation)
    {
        reservation = null;
        if (quantities.Any(entry => string.IsNullOrWhiteSpace(entry.Key)
            || !IsValidQuantity(entry.Key, entry.Value) || inventory.GetQuantity(entry.Key) < entry.Value))
            return false;


        var held = new Dictionary<string, decimal>(quantities, StringComparer.Ordinal);
        var values = new Dictionary<string, decimal>();
        foreach (var (itemId, reservedQuantity) in held)
        {
            decimal availableQuantity = inventory.GetQuantity(itemId);
            decimal availableValue = inventory.GetValue(itemId);
            values[itemId] = reservedQuantity == availableQuantity ? availableValue : availableValue * (reservedQuantity / availableQuantity);
        }

        reservation = new InventoryReservation(held, values);
        foreach (var entry in held)
        {
            inventory.Inventory[entry.Key] -= entry.Value;
            inventory.Values[entry.Key] = inventory.GetValue(entry.Key) - values[entry.Key];
        }

        inventory.Reservations.Add(reservation.Id, reservation);
        return true;
    }

    public bool TryRelease(InventoryComponent inventory, InventoryReservation reservation)
    {
        return TryExchange(inventory, reservation, reservation.Quantities, reservation.Values);
    }

    public bool TryExchange(
        InventoryComponent inventory,
        InventoryReservation reservation,
        IReadOnlyDictionary<string, decimal> outputs,
        IReadOnlyDictionary<string, decimal> outputValues)
    {
        if (!inventory.Reservations.TryGetValue(reservation.Id, out var held)
            || !ReferenceEquals(held, reservation) || outputs.Any(entry => string.IsNullOrWhiteSpace(entry.Key)
            || !IsValidQuantity(entry.Key, entry.Value)) || outputValues.Any(entry => entry.Value < 0
            || !outputs.ContainsKey(entry.Key)))
            return false;

        decimal afterCapacity = inventory.UsedCapacity - held.Quantities.Values.Sum() + outputs.Values.Sum();
        if (inventory.Capacity is decimal limit && afterCapacity > limit)
            return false;

        var quantities = new Dictionary<string, decimal>(inventory.Inventory, inventory.Inventory.Comparer);
        var values = new Dictionary<string, decimal>(inventory.Values, inventory.Values.Comparer);
        foreach (var output in outputs)
        {
            quantities[output.Key] = quantities.GetValueOrDefault(output.Key) + output.Value;
            values[output.Key] = values.GetValueOrDefault(output.Key) + outputValues.GetValueOrDefault(output.Key);
        }

        inventory.Inventory = quantities;
        inventory.Values = values;
        inventory.Reservations.Remove(held.Id);
        return true;
    }

    public bool TryConsume(Item item, InventoryComponent inventory)
    {
        if (inventory.GetQuantity(item.Id) < 1
            || !store.IsAlive(inventory.Parent)
            || store.GetComponent<CitizenComponent>(inventory.Parent) is not { Alive: true } citizen)
            return false;

        if (!consumptionService.ConsumeItem(item, citizen))
            return false;

        return TryRemove(item.Id, inventory, 1, out _);
    }
}
