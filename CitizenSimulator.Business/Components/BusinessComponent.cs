using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Business.Models;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Property.Components;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Business.Components;

public sealed class BusinessComponent : Component<BusinessComponent>
{
    public Models.Business Type { get; internal set; } = null!;
    public string Name { get; internal set; } = "";
    public CitizenComponent Owner { get; internal set; } = null!;
    public PropertyComponent Property { get; internal set; } = null!;
    public InventoryComponent Inventory { get; internal set; } = null!;
    public BankAccountComponent PaymentAccount { get; internal set; } = null!;
    public BankAccountComponent? OperatingCostRecipient { get; internal set; }
    public BusinessState State { get; internal set; } = BusinessState.SettingUp;
    public OperationalIssue Issue { get; internal set; }
    public OperationalIssue ProductionIssue { get; internal set; }
    public bool IsOperating => State == BusinessState.Open && Issue == OperationalIssue.None;
    public ProductionBatch? ActiveBatch { get; internal set; }
    internal List<EmploymentContract> Contracts { get; } = [];
    public IReadOnlyList<EmploymentContract> Employees => Contracts.AsReadOnly();
    public decimal SalesRevenue { get; internal set; }
    public decimal OwnerFunding { get; internal set; }
    public decimal OwnerWithdrawals { get; internal set; }
    public decimal StockPurchases { get; internal set; }
    public decimal CostOfGoodsSold { get; internal set; }
    public decimal ProductionLosses { get; internal set; }
    public decimal OperatingCostsAccrued { get; internal set; }
    public decimal UnpaidOperatingCosts { get; internal set; }
    public decimal OperatingCostsPaid { get; internal set; }
    public decimal WagesAccrued => Contracts.Sum(contract => contract.WagesAccrued);
    public decimal WagesPaid => Contracts.Sum(contract => contract.WagesPaid);
    public decimal UnpaidWages => Contracts.Sum(contract => contract.UnpaidWages);
    public decimal UtilityPurchases { get; internal set; }
    public decimal UtilityCostsConsumed { get; internal set; }
    public decimal DisposalCosts { get; internal set; }
    public decimal Expenses => CostOfGoodsSold + ProductionLosses + OperatingCostsAccrued + WagesAccrued + UtilityCostsConsumed + DisposalCosts;
    public decimal Profit => SalesRevenue - Expenses;
    public decimal TradingCashFlow => SalesRevenue - StockPurchases;
    public decimal OperatingCashFlow => TradingCashFlow - OperatingCostsPaid - WagesPaid - UtilityPurchases - DisposalCosts;
    public decimal NetCashFlow => OperatingCashFlow + OwnerFunding - OwnerWithdrawals;
    public decimal UnpaidLiabilities => UnpaidOperatingCosts + UnpaidWages;
    // Utility buffers use kWh/litres, separate from physical item storage.
    public decimal ElectricityKwh { get; internal set; }
    public decimal WaterLitres { get; internal set; }
    internal decimal ElectricityValue { get; set; }
    internal decimal WaterValue { get; set; }
    public bool GridConnected { get; internal set; }
    public decimal UtilityBufferHours { get; set; } = 24;
    public decimal DisposedTonnes { get; internal set; }
    internal decimal DisposedToday { get; set; }
    internal DateOnly DisposalDate { get; set; }
    internal TimeSpan ExportTime { get; set; } = TimeSpan.MinValue;
    internal decimal ExportedThisTick { get; set; }
    internal decimal CustomerCredits { get; set; }
    public decimal AvailableCustomerCapacity => CustomerCredits;
    internal decimal AvailableLaborMinutes { get; set; }
    internal decimal AvailableServiceMinutes { get; set; }
    internal TimeSpan? LastUpdatedAt { get; set; }
    internal TimeSpan? InsolventSince { get; set; }
}
