using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Models;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.Calender.Services.Abstractions;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Items.Services.Abstractions;
using CitizenSimulator.Market.Services.Abstractions;
using CitizenSimulator.Property.Components;
using CitizenSimulator.Utilities.Models;
using CitizenSimulator.Utilities.Services.Abstractions;
using CitizenSimulator.Vault.Components;
using CitizenSimulator.Vault.Services.Abstractions;

namespace CitizenSimulator.Utilities.Services;

public sealed partial class UtilityService(IEntityStore store, IBusinessService businesses,
    IMarketService market, IItemService items, IVaultService vault, ICalenderService calendar,
    SupplyConfiguration configuration) : IUtilityService
{
    public SupplyConfiguration Configuration { get; } = configuration;
    public BankAccountComponent? WorldAccount { get; private set; }
    public BankAccountComponent? StartupFund { get; private set; }

    private readonly Dictionary<EntityRef, UtilityConnection> _connections = [];
    private readonly HashSet<EntityRef> _disconnected = [];
    private readonly Dictionary<string, SupplyStatistics> _statistics = [];
    private readonly Dictionary<(EntityRef Consumer, string Resource, bool Home), decimal> _shortages = [];
    private readonly Dictionary<string, decimal> _itemImportsToday = [];
    private readonly Dictionary<string, decimal> _utilityImportsThisTick = [];
    private readonly Dictionary<string, decimal> _wasteExportsToday = [];
    private readonly Dictionary<string, decimal> _wasteExportedLifetime = [];
    private DateOnly _date;
    private TimeSpan _deliveryTime = TimeSpan.MinValue;
    private decimal _collectedToday;

    private TimeSpan Now => businesses.GameTime;
    private decimal PerTick(decimal hourlyRate)
    {
        // Multiply before dividing so a five-minute tick does not first round 1/12 hour.
        decimal duration = calendar.GetGameTime(1).Ticks;
        if (hourlyRate > decimal.MaxValue / duration)
            return hourlyRate / TimeSpan.TicksPerHour * duration;

        return hourlyRate * duration / TimeSpan.TicksPerHour;
    }
    private bool AccountsReady => WorldAccount != null && StartupFund != null
        && IsAttachedAccount(WorldAccount) && IsAttachedAccount(StartupFund);

    public void Advance(ulong tick, ICommandBuffer commands)
    {
        businesses.BeginTick(tick);
        RefreshLimits();
        AdvanceHomes();
        if (!EnsureAccounts(commands))
            return;

        foreach (BusinessComponent business in GetBusinesses())
        {
            if (!CanSupply(business))
                continue;

            if (Configuration.AutoConnectBusinesses && !_disconnected.Contains(business.Parent)
                && !_connections.ContainsKey(business.Parent))
                Connect(business.Parent, Configuration.ConnectionElectricityKw, Configuration.ConnectionWaterLitresPerHour);

            if (business.State == BusinessState.SettingUp)
                Bootstrap(business);
        }

        foreach (EntityRef entity in _connections.Keys.ToArray())
        {
            if (GetBusiness(entity) is not { } business || !CanSupply(business))
                _connections.Remove(entity);
        }

        foreach (var key in _shortages.Keys.ToArray())
        {
            bool validConsumer = key.Home ? IsUsableHome(key.Consumer)
                : GetBusiness(key.Consumer) is { } business && CanSupply(business);
            if (!validConsumer)
                _shortages.Remove(key);
        }
    }

    public bool Connect(EntityRef entity, decimal electricityKw, decimal waterLitresPerHour)
    {
        if (electricityKw < 0 || waterLitresPerHour < 0 || GetBusiness(entity) is not { } business
            || !CanSupply(business) || !businesses.TryConnectGrid(business))
            return false;

        if (!_connections.TryGetValue(entity, out var connection))
        {
            connection = new UtilityConnection { Business = entity };
            _connections.Add(entity, connection);
        }

        connection.ElectricityKw = electricityKw;
        connection.WaterLitresPerHour = waterLitresPerHour;
        connection.Connected = true;
        _disconnected.Remove(entity);
        return true;
    }

    public bool Disconnect(EntityRef entity)
    {
        if (GetBusiness(entity) is not { } business || !businesses.TryDisconnectGrid(business))
            return false;

        // Keep this tick's meter so reconnecting cannot reset delivery capacity.
        if (_connections.TryGetValue(entity, out var connection))
            connection.Connected = false;

        _disconnected.Add(entity);
        return true;
    }

    public UtilityConnection? GetConnection(EntityRef entity) => _connections.GetValueOrDefault(entity);

    public SupplyStatistics GetStatistics(string resourceId)
    {
        SupplyStatistics statistics = Statistics(resourceId);
        statistics.Unmet = _shortages.Where(entry => entry.Key.Resource == resourceId).Sum(entry => entry.Value);
        return statistics;
    }

    private SupplyStatistics Statistics(string resourceId)
    {
        if (!_statistics.TryGetValue(resourceId, out var statistics))
        {
            statistics = new SupplyStatistics();
            _statistics.Add(resourceId, statistics);
        }

        return statistics;
    }

    private void RefreshLimits()
    {
        DateOnly date = calendar.GetDate(Now);
        if (_date != date)
        {
            _date = date;
            _itemImportsToday.Clear();
            _wasteExportsToday.Clear();
            _collectedToday = 0;
        }

        if (_deliveryTime != Now)
        {
            _deliveryTime = Now;
            _utilityImportsThisTick.Clear();
        }
    }

    private bool EnsureAccounts(ICommandBuffer commands)
    {
        if (AccountsReady)
            return true;

        // discarded command buffers detach their components, clean the bank registrations too.
        if (WorldAccount != null && WorldAccount.Parent == default)
        {
            WorldAccount.Vault.Accounts.Remove(WorldAccount);
            StartupFund?.Vault.Accounts.Remove(StartupFund);
            WorldAccount = null;
            StartupFund = null;
        }

        if (WorldAccount != null)
            return false;

        VaultComponent? bank = store.Query(VaultComponent.GetComponentType())
            .Select(entity => store.GetComponent<VaultComponent>(entity)).FirstOrDefault();
        if (bank == null)
            return false;

        vault.AddBankAccount(bank, out var worldAccount);
        vault.AddBankAccount(bank, out var startupFund);
        WorldAccount = commands.AddComponent(commands.CreateEntity(), worldAccount);
        StartupFund = commands.AddComponent(commands.CreateEntity(), startupFund);
        vault.SetBalance(startupFund, Configuration.Startup.FundBalance);
        return false;
    }

    private bool IsAttachedAccount(BankAccountComponent account) => store.IsAlive(account.Parent)
        && ReferenceEquals(store.GetComponent<BankAccountComponent>(account.Parent), account)
        && account.Vault.Accounts.Contains(account);

    private BusinessComponent? GetBusiness(EntityRef entity) => store.IsAlive(entity)
        ? store.GetComponent<BusinessComponent>(entity) : null;

    private BusinessComponent[] GetBusinesses() => store.Query(BusinessComponent.GetComponentType())
        .OrderBy(entity => entity.Id).Select(entity => store.GetComponent<BusinessComponent>(entity)!).ToArray();

    private bool CanSupply(BusinessComponent business)
    {
        return business.State is not (BusinessState.Bankrupt or BusinessState.ShutDown)
               && store.IsAlive(business.Owner.Parent) && business.Owner.Alive
               && ReferenceEquals(store.GetComponent<CitizenComponent>(business.Owner.Parent), business.Owner)
               && store.IsAlive(business.Property.Parent)
               && ReferenceEquals(store.GetComponent<PropertyComponent>(business.Property.Parent), business.Property)
               && !business.Property.Destroyed && ReferenceEquals(business.Property.Owner, business.Owner);
    }

    private static decimal RoundDown(decimal quantity, decimal step)
    {
        return Math.Floor(quantity / step) * step;
    }

    private static decimal Affordable(BusinessComponent business, decimal requested, decimal unitPrice)
    {
        decimal balance = Math.Max(0, business.PaymentAccount.Balance - business.UnpaidLiabilities);
        if (unitPrice is > 0 and < 1 && requested * unitPrice <= balance)
            return requested;

        return unitPrice == 0 ? requested : Math.Min(requested, balance / unitPrice);
    }
}
