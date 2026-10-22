using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Utilities.Models;

public sealed class SupplyStatistics
{
    public decimal Requested { get; internal set; }
    public decimal LocalDelivered { get; internal set; }
    public decimal Imported { get; internal set; }
    public decimal ImportCost { get; internal set; }
    public decimal Unmet { get; internal set; }
    public decimal CollectedTonnes { get; internal set; }
    public decimal WorldDisposedTonnes { get; internal set; }
    public decimal DisposalFees { get; internal set; }
}

public sealed class UtilityConnection
{
    public required EntityRef Business { get; init; }
    public bool Connected { get; internal set; }
    public decimal ElectricityKw { get; internal set; }
    public decimal WaterLitresPerHour { get; internal set; }
    internal TimeSpan DeliveryTime { get; set; } = TimeSpan.MinValue;
    internal decimal ElectricityDelivered { get; set; }
    internal decimal WaterDelivered { get; set; }
}

public sealed class HomeUtilityConnection
{
    public required EntityRef House { get; init; }
    public bool Connected { get; internal set; } = true;
    public decimal ElectricityKwh { get; internal set; }
    public decimal WaterLitres { get; internal set; }
    public decimal Purchases { get; internal set; }
    public decimal ElectricityConsumed { get; internal set; }
    public decimal WaterConsumed { get; internal set; }
    public decimal UnservedElectricityKwh { get; internal set; }
    public decimal UnservedWaterLitres { get; internal set; }
    internal TimeSpan? AccountedUntil { get; set; }
    internal TimeSpan DeliveryTime { get; set; } = TimeSpan.MinValue;
    internal decimal ElectricityDelivered { get; set; }
    internal decimal WaterDelivered { get; set; }
}
