using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Models;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Business.Services;

public partial class BusinessService
{
    public bool TrySellUtility(BusinessComponent supplier, BankAccountComponent buyer, string utility,
        decimal requested, out decimal transferred, out decimal price)
    {
        transferred = 0;
        price = 0;
        if (requested <= 0 || !CanTradeExternally(supplier) || !supplier.GridConnected
            || supplier.State is not (BusinessState.Open or BusinessState.Closed)
            || supplier.Type.GridSupplyPolicy is not { } policy || policy.Utility != utility
            || utility is not ("electricity" or "water") || !buyer.Vault.Accounts.Contains(buyer)
            || !store.IsAlive(buyer.Parent) || !ReferenceEquals(store.GetComponent<BankAccountComponent>(buyer.Parent), buyer)
            || ReferenceEquals(buyer, supplier.PaymentAccount))
            return false;

        if (supplier.ExportTime != Now)
        {
            supplier.ExportTime = Now;
            supplier.ExportedThisTick = 0;
        }

        bool electricity = utility == "electricity";
        decimal available = electricity ? supplier.ElectricityKwh : supplier.WaterLitres;
        decimal rate = electricity ? policy.MaxExportKw : policy.MaxExportPerHour;
        decimal remaining = Math.Max(0, rate * calender.GetGameTime(1).Ticks / TimeSpan.TicksPerHour - supplier.ExportedThisTick);
        decimal amount = Math.Min(requested, Math.Min(available, remaining));
        bool affordable = policy.UnitPrice is > 0 and < 1 && amount * policy.UnitPrice <= buyer.Balance;
        if (policy.UnitPrice > 0 && !affordable)
            amount = Math.Min(amount, buyer.Balance / policy.UnitPrice);

        if (amount <= 0)
            return false;

        decimal cost = amount * policy.UnitPrice;
        decimal bufferValue = electricity ? supplier.ElectricityValue : supplier.WaterValue;
        decimal bookValue = bufferValue * (amount / available);
        _ = checked(supplier.SalesRevenue + cost);
        _ = checked(supplier.CostOfGoodsSold + bookValue);
        if (cost > 0 && !vaultService.TransferBalance(buyer, supplier.PaymentAccount, cost))
            return false;

        if (electricity)
        {
            supplier.ElectricityKwh -= amount;
            supplier.ElectricityValue -= bookValue;
        }
        else
        {
            supplier.WaterLitres -= amount;
            supplier.WaterValue -= bookValue;
        }

        supplier.ExportedThisTick += amount;
        RecordTrade(supplier, null, cost, bookValue);
        transferred = amount;
        price = cost;
        return true;
    }
}
