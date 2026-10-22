namespace CitizenSimulator.Utilities.Models;

public sealed class SupplyConfiguration
{
    public bool AutoConnectBusinesses { get; init; } = true;
    public decimal ConnectionElectricityKw { get; init; } = 1000;
    public decimal ConnectionWaterLitresPerHour { get; init; } = 10000;
    public decimal CollectionTonnesPerDay { get; init; } = 500;
    public StartupPolicy Startup { get; init; } = new();
    public HouseholdPolicy Households { get; init; } = new();
    public Dictionary<string, ImportPolicy> Items { get; init; } = [];
    public Dictionary<string, UtilityImportPolicy> Utilities { get; init; } = [];
    public Dictionary<string, WasteExportPolicy> Waste { get; init; } = [];
}

public sealed class HouseholdPolicy
{
    public bool AutoConnect { get; init; } = true;
    public decimal ElectricityKw { get; init; } = 20;
    public decimal WaterLitresPerHour { get; init; } = 1000;
    public decimal ElectricityKwhPerResidentPerDay { get; init; } = 8;
    public decimal WaterLitresPerResidentPerDay { get; init; } = 100;
    public decimal BufferHours { get; init; } = 24;
}

public sealed class ImportPolicy
{
    public bool Enabled { get; init; } = true;
    public string Unit { get; init; } = "";
    public decimal UnitPrice { get; init; }
    public decimal MaxPerDay { get; init; }
}

public sealed class UtilityImportPolicy
{
    public bool Enabled { get; init; } = true;
    public string Unit { get; init; } = "";
    public decimal UnitPrice { get; init; }
    public decimal MaxPerHour { get; init; }
}

public sealed class WasteExportPolicy
{
    public bool Enabled { get; init; }
    public decimal FeePerTonne { get; init; }
    public decimal MaxTonnesPerDay { get; init; }
    public decimal CapacityTonnes { get; init; }
}

public sealed class StartupPolicy
{
    public bool Enabled { get; init; } = true;
    public decimal FundBalance { get; init; } = 1000000000;
    public decimal WorkingCapitalDays { get; init; } = 7;
    public Dictionary<string, decimal> WorkingCapitalDaysByBusiness { get; init; } = [];
}
