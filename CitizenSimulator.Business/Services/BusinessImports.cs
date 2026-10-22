using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Models;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Business.Services;

public partial class BusinessService
{
    public bool TryFundStartup(BusinessComponent business, BankAccountComponent fund, decimal amount)
    {
        if (!CanTradeExternally(business) || business.State != BusinessState.SettingUp
            || business.StartupFundingAwarded || amount < 0 || !IsLiving(business.Owner)
            || !fund.Vault.Accounts.Contains(fund) || ReferenceEquals(fund, business.PaymentAccount))
            return false;

        if (amount > 0 && !vaultService.TransferBalance(fund, business.PaymentAccount, amount))
            return false;

        business.StartupFunding = amount;
        business.StartupFundingAwarded = true;
        return true;
    }

    public bool TryImportStock(BusinessComponent business, BankAccountComponent supplier,
        string itemId, decimal amount, decimal unitPrice)
    {
        if (!CanTradeExternally(business) || string.IsNullOrWhiteSpace(itemId) || amount <= 0 || unitPrice < 0
            || (unitPrice > 1 && amount > decimal.MaxValue / unitPrice))
            return false;

        decimal cost = checked(amount * unitPrice);
        if (business.StockPurchases > decimal.MaxValue - cost
            || business.Inventory.GetQuantity(itemId) > decimal.MaxValue - amount
            || business.Inventory.GetValue(itemId) > decimal.MaxValue - cost)
            return false;

        decimal purchases = business.StockPurchases + cost;
        if (!TryPayExternalSupplier(business, supplier, cost))
            return false;

        if (!inventoryService.TryAdd(itemId, business.Inventory, amount, cost))
        {
            ReverseImportPayment(business, supplier, cost);
            return false;
        }

        business.StockPurchases = purchases;
        return true;
    }

    public bool TryImportUtility(BusinessComponent business, BankAccountComponent supplier,
        string utility, decimal amount, decimal unitPrice)
    {
        if (!CanTradeExternally(business) || !business.GridConnected || amount <= 0 || unitPrice < 0
            || (unitPrice > 1 && amount > decimal.MaxValue / unitPrice)
            || utility is not ("electricity" or "water"))
            return false;

        decimal cost = checked(amount * unitPrice);
        decimal electricity = utility == "electricity" ? amount : 0;
        decimal water = utility == "water" ? amount : 0;
        decimal storedValue = utility == "electricity" ? business.ElectricityValue : business.WaterValue;
        if (business.UtilityPurchases > decimal.MaxValue - cost || storedValue > decimal.MaxValue - cost
            || business.ElectricityKwh > decimal.MaxValue - electricity || business.WaterLitres > decimal.MaxValue - water)
            return false;

        decimal purchases = business.UtilityPurchases + cost;
        if (!TryPayExternalSupplier(business, supplier, cost))
            return false;

        if (!TryDepositUtilities(business, electricity, water, cost))
        {
            ReverseImportPayment(business, supplier, cost);
            return false;
        }

        business.UtilityPurchases = purchases;
        return true;
    }

    public bool TryExportWaste(BusinessComponent business, BankAccountComponent recipient,
        string itemId, decimal amount, decimal fee)
    {
        if (!CanTradeExternally(business) || string.IsNullOrWhiteSpace(itemId) || amount <= 0 || fee < 0
            || !itemId.StartsWith("waste.", StringComparison.Ordinal) || business.Inventory.GetQuantity(itemId) < amount)
            return false;

        decimal bookValue = business.Inventory.GetValue(itemId) * (amount / business.Inventory.GetQuantity(itemId));
        if (business.DisposalCosts > decimal.MaxValue - fee || business.ProductionLosses > decimal.MaxValue - bookValue)
            return false;

        decimal costs = business.DisposalCosts + fee;
        decimal losses = business.ProductionLosses + bookValue;
        if (!TryPayExternalSupplier(business, recipient, fee))
            return false;

        if (!inventoryService.TryRemove(itemId, business.Inventory, amount, out _))
        {
            ReverseImportPayment(business, recipient, fee);
            return false;
        }

        business.DisposalCosts = costs;
        business.ProductionLosses = losses;
        return true;
    }

    public bool TryDisconnectGrid(BusinessComponent business)
    {
        if (!IsRegistered(business))
            return false;

        business.GridConnected = false;
        return true;
    }

    private bool CanTradeExternally(BusinessComponent business)
    {
        return IsRegistered(business) && !IsTerminal(business) && IsLiving(business.Owner)
            && HasValidPremises(business) && !business.Property.Destroyed
            && ReferenceEquals(store.GetComponent<InventoryComponent>(business.Parent), business.Inventory)
            && ReferenceEquals(store.GetComponent<BankAccountComponent>(business.Parent), business.PaymentAccount);
    }

    private bool TryPayExternalSupplier(BusinessComponent business, BankAccountComponent recipient, decimal cost)
    {
        if (ReferenceEquals(business.PaymentAccount, recipient) || !recipient.Vault.Accounts.Contains(recipient)
            || !business.PaymentAccount.Vault.Accounts.Contains(business.PaymentAccount)
            || cost > Math.Max(0, business.PaymentAccount.Balance - business.UnpaidLiabilities))
            return false;

        return cost == 0 || vaultService.TransferBalance(business.PaymentAccount, recipient, cost);
    }

    private void ReverseImportPayment(BusinessComponent business, BankAccountComponent recipient, decimal cost)
    {
        if (cost > 0 && !vaultService.TransferBalance(recipient, business.PaymentAccount, cost))
            throw new InvalidOperationException("Could not reverse a failed world delivery.");
    }
}
