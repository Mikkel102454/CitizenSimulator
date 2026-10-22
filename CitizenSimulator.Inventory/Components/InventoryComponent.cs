using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Inventory.Models;
using CitizenSimulator.Items.Models;

namespace CitizenSimulator.Inventory.Components;

public class InventoryComponent : Component<InventoryComponent>
{
    // Abstract storage units: one inventory quantity uses one unit.
    public decimal? Capacity { get; set; }
    public Dictionary<string, decimal> Inventory { get; set; } = [];
    internal Dictionary<string, decimal> Values { get; set; } = [];
    internal Dictionary<Guid, InventoryReservation> Reservations { get; } = [];
    public decimal UsedCapacity => Inventory.Values.Sum() + Reservations.Values.Sum(reservation => reservation.Quantities.Values.Sum());
    public decimal FreeCapacity => Capacity is decimal limit ? Math.Max(0, limit - UsedCapacity) : decimal.MaxValue;
    public decimal TotalValue => Values.Values.Sum() + Reservations.Values.Sum(reservation => reservation.Values.Values.Sum());

    public decimal GetQuantity(Item item)
    {
        return GetQuantity(item.Id);
    }

    public decimal GetQuantity(string itemId)
    {
        return Inventory.GetValueOrDefault(itemId);
    }

    public decimal GetValue(string itemId)
    {
        return Values.GetValueOrDefault(itemId);
    }

    public decimal GetReservedQuantity(string itemId)
    {
        return Reservations.Values.Sum(reservation => reservation.Quantities.GetValueOrDefault(itemId));
    }
}
