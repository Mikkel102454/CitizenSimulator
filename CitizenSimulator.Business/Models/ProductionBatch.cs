using CitizenSimulator.Inventory.Models;

namespace CitizenSimulator.Business.Models;

public sealed class ProductionBatch
{
    public Guid Id { get; } = Guid.NewGuid();
    public required Recipe Recipe { get; init; }
    public required IReadOnlyDictionary<string, decimal> ReservedInputs { get; init; }
    public required IReadOnlyDictionary<string, decimal> ReservedEquipment { get; init; }
    public required InventoryReservation Reservation { get; init; }
    public decimal MaterialValue { get; init; }
    public decimal ElapsedMinutes { get; internal set; }
    public decimal LaborMinutesWorked { get; internal set; }
    public bool IsReadyToComplete => ElapsedMinutes >= Recipe.DurationMinutes
        && LaborMinutesWorked >= Recipe.LaborMinutes;
}
