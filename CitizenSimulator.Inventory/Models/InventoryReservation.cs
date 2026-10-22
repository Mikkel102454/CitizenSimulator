using System.Collections.ObjectModel;

namespace CitizenSimulator.Inventory.Models;

public sealed class InventoryReservation
{
    public Guid Id { get; } = Guid.NewGuid();
    public IReadOnlyDictionary<string, decimal> Quantities { get; }
    public IReadOnlyDictionary<string, decimal> Values { get; }

    internal InventoryReservation(Dictionary<string, decimal> quantities, Dictionary<string, decimal> values)
    {
        Quantities = new ReadOnlyDictionary<string, decimal>(quantities);
        Values = new ReadOnlyDictionary<string, decimal>(values);
    }
}
