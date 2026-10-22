using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Models;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.Calender.Services.Abstractions;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Inventory.Services.Abstractions;
using CitizenSimulator.Items.Services.Abstractions;
using CitizenSimulator.Property.Components;
using CitizenSimulator.Vault.Components;
using CitizenSimulator.Vault.Services.Abstractions;
using BusinessDefinition = CitizenSimulator.Business.Models.Business;

namespace CitizenSimulator.Business.Services;

public partial class BusinessService(IEntityStore store,
    IEventBus events,
    IVaultService vaultService,
    IInventoryService inventoryService,
    ICalenderService calender,
    ISimulationClock clock,
    IItemService items) : IBusinessService
{
    private readonly Dictionary<EntityRef, BusinessComponent> _pendingCreations = [];
    private TimeSpan _observedGameTime;
    private TimeSpan Now
    {
        get
        {
            var clockTime = calender.GetGameTime(clock.Tick);
            return clockTime > _observedGameTime ? clockTime : _observedGameTime;
        }
    }

    public void BeginTick(ulong tick)
    {
        _observedGameTime = calender.GetGameTime(tick);
    }

    private bool IsRegistered(BusinessComponent business)
    {
        return store.IsAlive(business.Parent)
            && ReferenceEquals(store.GetComponent<BusinessComponent>(business.Parent), business);
    }

    private bool IsLiving(CitizenComponent citizen)
    {
        return citizen.Alive
            && store.IsAlive(citizen.Parent)
            && ReferenceEquals(store.GetComponent<CitizenComponent>(citizen.Parent), citizen);
    }

    private static bool IsTerminal(BusinessComponent business)
    {
        return business.State is BusinessState.Bankrupt or BusinessState.ShutDown;
    }

    private decimal GetRequiredCapital(BusinessComponent business)
    {
        var costs = business.Type.OperatingCosts;
        decimal hourlyPayroll = business.Contracts
            .Where(contract => contract.IsEmployed)
            .Sum(contract => contract.HourlyWage);
        decimal operatingReserve = costs.InsurancePerDay + costs.MaintenanceBudgetPerDay + hourlyPayroll;

        return business.UnpaidLiabilities + Math.Max(business.Type.MinimumCapital, operatingReserve);
    }

    public IReadOnlyList<BusinessDefinition> GetBusinessTypes()
    {
        return BusinessDefinition.BusinessList.AsReadOnly();
    }

    public IReadOnlyList<Recipe> GetRecipes()
    {
        return Recipe.RecipeList.AsReadOnly();
    }

    private bool HasValidPremises(BusinessComponent business)
    {
        return !business.Property.Destroyed
            && store.IsAlive(business.Property.Parent)
            && ReferenceEquals(store.GetComponent<PropertyComponent>(business.Property.Parent), business.Property)
            && ReferenceEquals(business.Property.Owner, business.Owner)
            && business.Property.AreaM2 >= business.Type.Premises.MinimumAreaM2
            && business.Type.Premises.AllowedUses.Contains(business.Property.Type.ToString(), StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, decimal> GetRequiredEquipment(IEnumerable<EquipmentRequirement> requirements)
    {
        return requirements
            .GroupBy(requirement => requirement.EquipmentId)
            .ToDictionary(group => group.Key, group => group.Sum(requirement => (decimal)requirement.Count));
    }

    private bool HasRequiredEquipment(BusinessComponent business)
    {
        foreach (var (itemId, requiredQuantity) in GetRequiredEquipment(business.Type.Equipment))
        {
            decimal availableQuantity = business.Inventory.GetQuantity(itemId);
            decimal reservedQuantity = business.ActiveBatch?.ReservedEquipment.GetValueOrDefault(itemId) ?? 0;
            if (availableQuantity + reservedQuantity < requiredQuantity)
                return false;
        }

        return true;
    }

    public void ReconcileCreations()
    {
        foreach (var (entity, business) in _pendingCreations.ToArray())
        {
            if (IsRegistered(business))
            {
                business.Inventory.Capacity = business.Type.Premises.StorageCapacity;
                _pendingCreations.Remove(entity);
            }
            else if (business.Parent == default)
            {
                business.PaymentAccount.Vault.Accounts.Remove(business.PaymentAccount);
                _pendingCreations.Remove(entity);
            }
        }
    }

    public bool CreateBusiness(ICommandBuffer commands, string typeId, string name, CitizenComponent owner, PropertyComponent property, out BusinessComponent? business)
    {
        ReconcileCreations();
        business = null;
        if (string.IsNullOrWhiteSpace(name) || !IsLiving(owner) || !store.IsAlive(property.Parent)
            || !ReferenceEquals(store.GetComponent<PropertyComponent>(property.Parent), property)
            || !ReferenceEquals(property.Owner, owner) || property.Destroyed
            || store.HasComponent<BusinessComponent>(property.Parent) || store.HasComponent<BankAccountComponent>(property.Parent)
            || _pendingCreations.ContainsKey(property.Parent))
            return false;

        var type = BusinessDefinition.BusinessList.Find(definition => definition.Id == typeId);
        if (type == null
            || property.AreaM2 < type.Premises.MinimumAreaM2
            || !type.Premises.AllowedUses.Contains(property.Type.ToString(), StringComparer.OrdinalIgnoreCase))
            return false;

        var vault = store.Query(VaultComponent.GetComponentType())
            .Select(entity => store.GetComponent<VaultComponent>(entity)).FirstOrDefault();
        if (vault == null)
            return false;

        var inventory = store.GetComponent<InventoryComponent>(property.Parent);
        if (inventory != null && inventory.UsedCapacity > type.Premises.StorageCapacity)
            return false;

        if (!vaultService.AddBankAccount(vault, out var account))
            return false;

        commands.AddComponent(property.Parent, account);
        business = commands.AddComponent(property.Parent, new BusinessComponent
        {
            Type = type,
            Name = name,
            Owner = owner,
            Property = property,
            PaymentAccount = account,
            Inventory = inventory ?? commands.AddComponent(property.Parent,
                new InventoryComponent { Capacity = type.Premises.StorageCapacity })
        });
        _pendingCreations.Add(property.Parent, business);
        return true;
    }

    public bool TryActivateBusiness(BusinessComponent business)
    {
        if (!IsRegistered(business)
            || business.State is not (BusinessState.SettingUp or BusinessState.Suspended)
            || !IsLiving(business.Owner)
            || !HasValidPremises(business)
            || !HasRequiredEquipment(business)
            || !HasRequiredContracts(business)
            || business.PaymentAccount.Balance < GetRequiredCapital(business))
            return false;

        var costs = business.Type.OperatingCosts;
        bool hasOperatingCosts = costs.InsurancePerDay + costs.MaintenanceBudgetPerDay > 0;
        if (hasOperatingCosts && business.OperatingCostRecipient == null)
            return false;

        business.State = BusinessState.Closed;
        business.Issue = OperationalIssue.None;
        business.LastUpdatedAt = Now;
        business.InsolventSince = null;
        return true;
    }

    private bool HasRequiredContracts(BusinessComponent business)
    {
        foreach (var requirement in business.Type.Workforce)
        {
            int employeeCount = business.Contracts.Count(contract =>
                contract.IsEmployed && IsLiving(contract.Employee) && contract.Role == requirement.Role);
            if (employeeCount < requirement.DesiredCount)
                return false;
        }

        return true;
    }

    public bool TrySuspendBusiness(BusinessComponent business)
    {
        if (!IsRegistered(business) || business.State is not (BusinessState.Open or BusinessState.Closed))
            return false;

        RecordAttendance(business, Now);
        business.State = BusinessState.Suspended;
        foreach (var contract in business.Contracts)
        {
            contract.IsCheckedIn = false;
        }

        return true;
    }

    public bool TryCloseBusiness(BusinessComponent business)
    {
        if (!IsRegistered(business) || IsTerminal(business) || (business.ActiveBatch != null && !TryCancelBatch(business)))
            return false;

        RecordAttendance(business, Now);
        foreach (var contract in business.Contracts)
        {
            contract.IsCheckedIn = false;
            contract.IsEmployed = false;
        }

        business.State = BusinessState.ShutDown;
        return true;
    }

    public bool TryDeclareBankrupt(BusinessComponent business)
    {
        if (!IsRegistered(business) || business.State != BusinessState.Suspended || business.UnpaidLiabilities <= business.PaymentAccount.Balance)
            return false;

        business.State = BusinessState.Bankrupt;
        foreach (var contract in business.Contracts)
        {
            contract.IsCheckedIn = false;
            contract.IsEmployed = false;
        }

        return true;
    }

    public bool TryLiquidateBusiness(ICommandBuffer commands, BusinessComponent business)
    {
        if (!IsRegistered(business) || !IsTerminal(business) || business.UnpaidLiabilities != 0
            || business.ElectricityKwh != 0 || business.WaterLitres != 0 || !IsLiving(business.Owner))
            return false;

        var receiver = store.GetComponent<InventoryComponent>(business.Owner.Parent);
        var account = store.GetComponent<BankAccountComponent>(business.Owner.Parent);
        if (receiver == null || account == null || ReferenceEquals(receiver, business.Inventory)
            || ReferenceEquals(account, business.PaymentAccount) || receiver.FreeCapacity < business.Inventory.UsedCapacity
            || account.Balance > decimal.MaxValue - business.PaymentAccount.Balance)
            return false;

        _ = business.OwnerWithdrawals + business.PaymentAccount.Balance;
        if (business.ActiveBatch != null && !TryCancelBatch(business))
            return false;

        foreach (var item in business.Inventory.Inventory.Where(entry => entry.Value > 0).ToArray())
        {
            decimal value = business.Inventory.GetValue(item.Key);
            if (!inventoryService.TryTransfer(item.Key, business.Inventory, receiver, item.Value, value, out _))
                throw new InvalidOperationException("Prevalidated asset distribution failed.");

        }

        if (business.PaymentAccount.Balance > 0 && !TryWithdrawOwnerFunds(business, business.PaymentAccount.Balance))
            return false;

        business.PaymentAccount.Vault.Accounts.Remove(business.PaymentAccount);
        commands.RemoveComponent<BankAccountComponent>(business.Parent);
        commands.RemoveComponent<BusinessComponent>(business.Parent);
        return true;
    }

    public bool TryFundBusiness(BusinessComponent business, decimal amount)
    {
        if (amount <= 0 || !IsRegistered(business) || !IsLiving(business.Owner))
            return false;

        var account = store.GetComponent<BankAccountComponent>(business.Owner.Parent);
        decimal funding = business.OwnerFunding + amount;
        if (account == null || !vaultService.TransferBalance(account, business.PaymentAccount, amount))
            return false;

        business.OwnerFunding = funding;
        return true;
    }

    public bool TryWithdrawOwnerFunds(BusinessComponent business, decimal amount)
    {
        if (amount <= 0 || !IsRegistered(business) || !IsLiving(business.Owner) || business.PaymentAccount.Balance - amount < business.UnpaidLiabilities)
            return false;

        var account = store.GetComponent<BankAccountComponent>(business.Owner.Parent);
        decimal withdrawn = business.OwnerWithdrawals + amount;
        if (account == null || !vaultService.TransferBalance(business.PaymentAccount, account, amount))
            return false;

        business.OwnerWithdrawals = withdrawn;
        return true;
    }

    public bool TrySetOperatingCostRecipient(BusinessComponent business, BankAccountComponent recipient)
    {
        if (!IsRegistered(business) || ReferenceEquals(business.PaymentAccount, recipient) || !recipient.Vault.Accounts.Contains(recipient))
            return false;

        business.OperatingCostRecipient = recipient;
        return true;
    }

    public bool TryPayOperatingCosts(BusinessComponent business, BankAccountComponent recipient, decimal amount)
    {
        if (amount <= 0 || !IsRegistered(business) || amount > business.UnpaidOperatingCosts || ReferenceEquals(business.PaymentAccount, recipient))
            return false;

        decimal paid = business.OperatingCostsPaid + amount;
        if (!vaultService.TransferBalance(business.PaymentAccount, recipient, amount))
            return false;


        business.OperatingCostsPaid = paid;
        business.UnpaidOperatingCosts -= amount;
        return true;
    }

    public void RecordTrade(BusinessComponent seller, BusinessComponent? buyer, decimal price, decimal costOfGoods)
    {
        if (price < 0 || costOfGoods < 0)
            throw new ArgumentOutOfRangeException(nameof(price));


        decimal revenue = seller.SalesRevenue + price;
        decimal cost = seller.CostOfGoodsSold + costOfGoods;
        decimal purchases = (buyer?.StockPurchases ?? 0) + price;
        seller.SalesRevenue = revenue;
        seller.CostOfGoodsSold = cost;
        if (buyer != null)
            buyer.StockPurchases = purchases;

    }

    public decimal GetSalePrice(BusinessComponent seller, string itemId)
    {
        decimal configured = seller.Type.SalesPolicy!.SellItems[itemId];
        decimal quantity = seller.Inventory.GetQuantity(itemId);
        decimal costPrice = quantity > 0 ? seller.Inventory.GetValue(itemId) / quantity : 0;
        return Math.Max(configured, costPrice * (1 + seller.Type.SalesPolicy.TargetMarkupPercent / 100));
    }

    public bool TryUseCustomerCapacity(BusinessComponent business)
    {
        if (business.Type.SalesPolicy?.CustomersServedPerWorkerHour is not > 0 || !business.Type.Workforce.Any(requirement => requirement.DesiredCount > 0))
            return true;

        if (business.CustomerCredits < 1)
            return false;

        business.CustomerCredits -= 1;
        return true;
    }
}
