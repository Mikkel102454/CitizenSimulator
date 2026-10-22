using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Models;

namespace CitizenSimulator.Business.Services;

public partial class BusinessService
{
    private static bool CanConsumeUtilities(BusinessComponent business, decimal electricity, decimal water)
    {
        return electricity >= 0 && water >= 0 && business.ElectricityKwh >= electricity && business.WaterLitres >= water;
    }

    private static void ConsumeUtilities(BusinessComponent business, decimal electricity, decimal water)
    {
        decimal electricityValue = electricity == 0 ? 0 : business.ElectricityValue * (electricity / business.ElectricityKwh);
        decimal waterValue = water == 0 ? 0 : business.WaterValue * (water / business.WaterLitres);
        business.UtilityCostsConsumed += electricityValue + waterValue;
        business.ElectricityKwh -= electricity;
        business.WaterLitres -= water;
        business.ElectricityValue -= electricityValue;
        business.WaterValue -= waterValue;
    }

    // Explicit scenario/import delivery. The runtime never calls this to invent supply.
    public bool TryDepositUtilities(BusinessComponent business, decimal electricityKwh, decimal waterLitres, decimal totalValue = 0)
    {
        if (!IsRegistered(business) || electricityKwh < 0 || waterLitres < 0 || totalValue < 0 || electricityKwh + waterLitres <= 0)
            return false;

        decimal electricity = business.ElectricityKwh + electricityKwh;
        decimal water = business.WaterLitres + waterLitres;
        decimal energyValue = totalValue * (electricityKwh / (electricityKwh + waterLitres));
        decimal waterValue = business.WaterValue + totalValue - energyValue;
        energyValue += business.ElectricityValue;
        business.ElectricityKwh = electricity;
        business.WaterLitres = water;
        business.ElectricityValue = energyValue;
        business.WaterValue = waterValue;
        return true;
    }

    public bool TryConnectGrid(BusinessComponent business)
    {
        if (!IsRegistered(business) || IsTerminal(business) || business.Property.Destroyed)
            return false;

        business.GridConnected = true;
        return true;
    }

    public bool TryTransferUtility(BusinessComponent supplier,
        BusinessComponent receiver,
        string utility,
        decimal requested,
        out decimal transferred)
    {
        transferred = 0;
        if (requested <= 0
            || !IsRegistered(supplier)
            || !IsRegistered(receiver)
            || ReferenceEquals(supplier, receiver)
            || IsTerminal(receiver)
            || supplier.State is not (BusinessState.Open or BusinessState.Closed or BusinessState.Bankrupt or BusinessState.ShutDown)
            || !receiver.GridConnected
            || supplier.Type.GridSupplyPolicy is not { } policy
            || policy.Utility != utility
            || (policy.ConnectionRequired && !supplier.GridConnected))
            return false;

        bool isElectricity = utility == "electricity";
        if (!isElectricity && utility != "water")
            return false;

        decimal available = isElectricity ? supplier.ElectricityKwh : supplier.WaterLitres;
        decimal value = isElectricity ? supplier.ElectricityValue : supplier.WaterValue;
        decimal rate = isElectricity ? policy.MaxExportKw : policy.MaxExportPerHour;
        if (supplier.ExportTime != Now)
        {
            supplier.ExportTime = Now;
            supplier.ExportedThisTick = 0;
        }

        decimal exportLimit = rate * calender.GetGameTime(1).Ticks / TimeSpan.TicksPerHour;
        decimal remainingExportCapacity = Math.Max(0, exportLimit - supplier.ExportedThisTick);
        decimal availableSupply = Math.Min(available, remainingExportCapacity);
        decimal quantity = Math.Min(requested, availableSupply);
        if (policy.UnitPrice > 0)
        {
            decimal spendableBalance = Math.Max(0, receiver.PaymentAccount.Balance - receiver.UnpaidLiabilities);
            decimal affordableQuantity = spendableBalance / policy.UnitPrice;
            quantity = Math.Min(quantity, affordableQuantity);
        }

        if (quantity <= 0)
            return false;

        decimal cost = quantity * policy.UnitPrice;
        decimal costBasis = value * (quantity / available);
        decimal received = (isElectricity ? receiver.ElectricityKwh : receiver.WaterLitres) + quantity;
        decimal receivedValue = (isElectricity ? receiver.ElectricityValue : receiver.WaterValue) + cost;
        decimal purchases = receiver.UtilityPurchases + cost;
        _ = supplier.SalesRevenue + cost;
        _ = supplier.CostOfGoodsSold + costBasis;
        if (cost > 0 && !vaultService.TransferBalance(receiver.PaymentAccount, supplier.PaymentAccount, cost))
            return false;

        if (isElectricity)
        {
            supplier.ElectricityKwh -= quantity;
            supplier.ElectricityValue -= costBasis;
            receiver.ElectricityKwh = received;
            receiver.ElectricityValue = receivedValue;
        }
        else
        {
            supplier.WaterLitres -= quantity;
            supplier.WaterValue -= costBasis;
            receiver.WaterLitres = received;
            receiver.WaterValue = receivedValue;
        }

        RecordTrade(supplier, null, cost, costBasis);
        receiver.UtilityPurchases = purchases;
        supplier.ExportedThisTick += quantity;
        transferred = quantity;
        return true;
    }

    public decimal GetDisposalCapacity(BusinessComponent business, string itemId)
    {
        if (!IsRegistered(business) || !business.IsOperating || business.Type.DisposalPolicy is not { } policy
            || !policy.AcceptedItems.Contains(itemId))
            return 0;

        decimal mass = items.GetItems().FirstOrDefault(item => item.Id == itemId)?.MassKgPerUnit ?? 0;
        if (mass <= 0)
            return 0;

        decimal today = business.DisposalDate == calender.GetDate(Now) ? business.DisposedToday : 0;
        decimal dailyCapacityLeft = policy.MaxIntakeTonnesPerDay - today;
        decimal lifetimeCapacityLeft = policy.CapacityTonnes - business.DisposedTonnes;
        decimal tonnes = Math.Max(0, Math.Min(dailyCapacityLeft, lifetimeCapacityLeft));
        return Math.Min(business.Inventory.FreeCapacity, tonnes * 1000 / mass);
    }

    public bool TryDisposeWaste(BusinessComponent source, BusinessComponent disposalBusiness, string itemId, decimal amount)
    {
        if (amount <= 0 || !IsRegistered(source) || !IsRegistered(disposalBusiness) || ReferenceEquals(source, disposalBusiness)
            || !disposalBusiness.IsOperating || disposalBusiness.Type.DisposalPolicy is not { } policy || !policy.AcceptedItems.Contains(itemId)
            || source.Inventory.GetQuantity(itemId) < amount)
            return false;

        var date = calender.GetDate(Now);
        decimal today = disposalBusiness.DisposalDate == date ? disposalBusiness.DisposedToday : 0;
        decimal mass = items.GetItems().FirstOrDefault(item => item.Id == itemId)?.MassKgPerUnit ?? 0;
        if (mass <= 0)
            return false;

        decimal tonnes = amount * mass / 1000;
        if (amount > GetDisposalCapacity(disposalBusiness, itemId))
            return false;

        decimal fee = tonnes * policy.FeePerTonne;

        // Check ledger arithmetic before moving money or inventory.
        _ = source.DisposalCosts + fee;
        _ = source.ProductionLosses + source.Inventory.GetValue(itemId) * (amount / source.Inventory.GetQuantity(itemId));
        _ = disposalBusiness.SalesRevenue + fee;

        decimal sourceBalanceBefore = source.PaymentAccount.Balance;
        decimal disposalBalanceBefore = disposalBusiness.PaymentAccount.Balance;
        if (fee > 0 && !vaultService.TransferBalance(source.PaymentAccount, disposalBusiness.PaymentAccount, fee))
            return false;

        if (!inventoryService.TryTransfer(itemId, source.Inventory, disposalBusiness.Inventory, amount, 0, out var lostValue))
        {
            vaultService.SetBalance(source.PaymentAccount, sourceBalanceBefore);
            vaultService.SetBalance(disposalBusiness.PaymentAccount, disposalBalanceBefore);
            return false;
        }

        source.DisposalCosts += fee;
        source.ProductionLosses += lostValue;
        disposalBusiness.DisposedTonnes += tonnes;
        disposalBusiness.DisposedToday = today + tonnes;
        disposalBusiness.DisposalDate = date;
        RecordTrade(disposalBusiness, null, fee, 0);
        return true;
    }
}
