using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Models;
using CitizenSimulator.Business.Services;
using CitizenSimulator.Business.Systems;
using CitizenSimulator.Calender.Services;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Citizen.Services;
using CitizenSimulator.CitizenMind.Components;
using CitizenSimulator.Engine.ECS;
using CitizenSimulator.Engine.Events;
using CitizenSimulator.Engine.Modding;
using CitizenSimulator.Engine.Services;
using CitizenSimulator.Engine.Timing;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Inventory.Services;
using CitizenSimulator.Items.Services.Abstractions;
using CitizenSimulator.Market.Services;
using CitizenSimulator.Market.Services.Abstractions;
using CitizenSimulator.Market.Systems;
using CitizenSimulator.Needs.Components;
using CitizenSimulator.Needs.Services;
using CitizenSimulator.Needs.Services.Abstractions;
using CitizenSimulator.Property.Components;
using CitizenSimulator.Property.Services;
using CitizenSimulator.Vault.Components;
using CitizenSimulator.Vault.Services;

int checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition)
        throw new Exception(message);
}

void Near(decimal actual,
    decimal expected,
    string message) => Check(Math.Abs(actual - expected) < 0.00000001m, $"{message}: expected {expected}, got {actual}");
void Scenario(string name, Action<World> run, bool procurement = false)
{
    using var world = new World(procurement);
    run(world);
    Console.WriteLine("PASS " + name);
}

Scenario("catalog and quantity validation", world =>
{
    Check(world.Businesses.GetBusinessTypes().Count >= 8, "All catalog businesses must load");
    var stock = new InventoryComponent
    {
        Capacity = 5
    };
    Check(!world.Inventory.TryAdd("equipment.oven", stock, 0.5m), "Half an oven must be rejected");
    Check(!world.Inventory.TryAdd("unknown", stock, 1), "Unknown items must be rejected");
    Check(world.Inventory.TryAdd("material.flour", stock, 2.125m, 10), "Fractional material must be supported");
    Check(!world.Inventory.TryAdd("material.flour", stock, -1), "Negative addition must fail");
    var hours = new OpeningHours
    {
        Days = ["monday"],
        Opens = "22:00",
        Closes = "02:00"
    };
    Check(hours.IsOpen(DayOfWeek.Monday, new(22, 0)), "Opening inclusive");
    Check(hours.IsOpen(DayOfWeek.Tuesday, new(1, 59)), "Overnight tail");
    Check(!hours.IsOpen(DayOfWeek.Tuesday, new(2, 0)), "Closing exclusive");
    var shifts = new[]
    {
        hours,
        hours
    };
    var monday = TimeSpan.FromDays(2) + TimeSpan.FromHours(22);
    Near(BusinessTime.OverlapMinutes(shifts, monday, monday + TimeSpan.FromHours(4), world.Calendar), 240, "Overlapping hours must not duplicate time");
    var bad = new Recipe
    {
        Id = "bad",
        DurationMinutes = 1,
        Outputs = new()
        {
            ["unknown"] = 1
        }
    };
    bool rejected = false;
    try
    {
        BusinessCatalog.Validate([bad], [], world.Items.GetItems().Select(i => i.Id).ToHashSet());
    }
    catch (InvalidOperationException)
    {
        rejected = true;
    }

    Check(rejected, "Unknown recipe references must fail validation");
});
Scenario("reservation capacity, valuation, and duplicate release", world =>
{
    var stock = new InventoryComponent
    {
        Capacity = 3
    };
    world.Inventory.TryAdd("material.flour", stock, 2, 10);
    world.Inventory.TryAdd("equipment.oven", stock, 1, 100);
    Check(world.Inventory.TryReserve(stock, new Dictionary<string, decimal> { ["material.flour"] = 2, ["equipment.oven"] = 1 }, out var hold), "Reservation");
    Near(stock.UsedCapacity, 3, "Reserved goods must retain storage occupancy");
    Near(stock.TotalValue, 110, "Reservation must conserve value");
    Check(!world.Inventory.TryAdd("food.bread", stock, 1), "Held stock must prevent overfill");
    Check(!world.Inventory.TryRemove("equipment.oven", stock, 1, out _), "Held equipment cannot be sold");
    Check(world.Inventory.TryRelease(stock, hold!), "Release");
    Check(!world.Inventory.TryRelease(stock, hold!), "Release exactly once");
    Near(stock.TotalValue, 110, "Release must conserve value");
});
Scenario("duplicate business creation in one command buffer", world =>
{
    var owner = world.Citizen();
    var property = world.Property(owner, PropertyComponent.PropertyType.Commercial, 100);
    int before = world.Vault.Accounts.Count;
    BusinessComponent? created = null;
    world.Do(commands =>
    {
        Check(world.Businesses.CreateBusiness(commands, "business.bakery", "first", owner, property, out created), "First create");
        Check(!world.Businesses.CreateBusiness(commands, "business.bakery", "second", owner, property, out _), "Pending duplicate");
    });
    Check(created != null && world.Vault.Accounts.Count == before + 1, "Only one business account");
    var wrong = world.Property(owner, PropertyComponent.PropertyType.Residential, 100);
    world.Do(commands => Check(!world.Businesses.CreateBusiness(commands, "business.bakery", "wrong", owner, wrong, out _), "Wrong permitted use"));
});
Scenario("attendance, paid labor, production, and profit", world =>
{
    var business = world.Make("business.bakery");
    Check(!world.Businesses.TryActivateBusiness(business), "Unfunded/unequipped/unhired business must fail readiness");
    world.Prepare(business);
    world.Inventory.TryAdd("material.flour", business.Inventory, 2, 10);
    world.Tick();
    Check(business.ActiveBatch == null && business.Issue == OperationalIssue.MissingStaff, "Hiring alone must not invent attendance");
    Check(!world.Businesses.TryAddBatchLabor(business, 1000), "Direct calls cannot invent labor");
    world.CheckIn(business);
    world.Tick(11);
    Near(business.Inventory.GetQuantity("food.bread"), 4, "Bakery output");
    Near(business.Inventory.GetQuantity("waste.organic"), 0.1m, "Bakery waste");
    Near(business.Inventory.GetQuantity("equipment.oven"), 1, "Equipment returned at completion");
    Near(business.Inventory.GetValue("food.bread"), 10, "Materials carried into output valuation");
    Check(business.WagesPaid > 0 && business.UnpaidWages == 0, "Recorded attendance must pay wages");
    foreach (var contract in business.Employees)
    {
        Near(contract.WagesPaid, contract.MinutesWorked * contract.HourlyWage / 60, "Pay from actual attendance");
    }

    var client = world.Citizen();
    var account = world.Account(client);
    var inventory = world.Stock(client);
    decimal totalCash = world.Cash();
    Check(world.Market.TryBuy(inventory, account, "food.bread", 1) == IMarketService.BuyStatus.SUCCESS, "Retail purchase");
    Near(world.Cash(), totalCash, "Purchase conserves money");
    Near(business.SalesRevenue, 13, "Revenue");
    Near(business.CostOfGoodsSold, 2.5m, "Weighted material cost");
    Near(business.Profit, business.SalesRevenue - business.Expenses, "Accrual profit");
    Near(business.NetCashFlow, business.PaymentAccount.Balance, "Tracked cash flow matches funded account");
    decimal revenue = business.SalesRevenue;
    world.Tick();
    Near(business.SalesRevenue, revenue, "Event delivery must not duplicate accounting");
});
Scenario("blocked output retry, cancellation, and suspension", world =>
{
    var business = world.Make("business.bakery");
    world.Prepare(business);
    business.Inventory.Capacity = 3;
    world.Inventory.TryAdd("material.flour", business.Inventory, 2, 10);
    world.Tick();
    world.CheckIn(business);
    world.Tick(11);
    Check(business.ActiveBatch is { IsReadyToComplete: true }, "Full output must remain pending");
    Check(business.ProductionIssue == OperationalIssue.OutputStorageFull
        && business.IsOperating, "Output blockage must not disable sales that free storage");
    Near(business.Inventory.GetQuantity("food.bread"), 0, "No partial output");
    Near(business.Inventory.UsedCapacity, 3, "Held input/equipment capacity");
    business.Inventory.Capacity = 6;
    world.Tick();
    Near(business.Inventory.GetQuantity("food.bread"), 4, "Retry completion after space added");
    Check(!world.Businesses.TryCompleteBatch(business), "Cannot complete twice");
    world.Inventory.TryRemove("food.bread", business.Inventory, 4, out _);
    world.Inventory.TryRemove("waste.organic", business.Inventory, 0.1m, out _);
    world.Inventory.TryAdd("material.flour", business.Inventory, 2, 10);
    world.Tick();
    Check(business.ActiveBatch != null, "Next batch starts automatically");
    var batch = business.ActiveBatch!;
    Check(world.Businesses.TrySuspendBusiness(business), "Suspend");
    decimal elapsed = batch.ElapsedMinutes;
    world.Tick(3);
    Near(batch.ElapsedMinutes, elapsed, "Suspension must pause batch");
    Check(world.Businesses.TryCancelBatch(business), "Cancellation");
    Near(business.Inventory.GetQuantity("material.flour"), 2, "Reserved inputs returned");
    Check(!world.Businesses.TryCancelBatch(business), "Cancellation cannot duplicate inputs");
});
Scenario("calendar time remains distinct from shift work", world =>
{
    var business = world.Make("business.wheat_farm");
    world.Prepare(business);
    world.Businesses.TryDepositUtilities(business, 100000, 100000);
    world.Tick();
    world.CheckIn(business);
    world.Tick();
    Check(business.ActiveBatch != null, "Farm batch starts");
    world.Jump(288UL * 90);
    Near(business.Inventory.GetQuantity("material.wheat"), 100, "Ninety-day crop finishes");
    Check(business.Employees.Sum(contract => contract.MinutesWorked) <= 18 * 60, "One check-in must not generate attendance on future days");
});
Scenario("utility production, export cap, and real billing", world =>
{
    var plant = world.Make("business.waste_to_energy_facility");
    world.Prepare(plant, 10, 10000);
    world.Inventory.TryAdd("waste.organic", plant.Inventory, 100, 10);
    world.Tick();
    world.CheckIn(plant);
    world.Tick(14);
    Check(plant.ElectricityKwh > 30, "Utility-producing batches must complete");
    Near(plant.Inventory.GetQuantity("waste.ash"), 10, "Incinerator ash");
    Near(plant.ProductionLosses, 10, "Utility feedstock cost recognized");
    var receiver = world.Make("business.bakery");
    world.Businesses.TryFundBusiness(receiver, 1000);
    world.Businesses.TryConnectGrid(receiver);
    decimal cash = world.Cash();
    Check(world.Businesses.TryTransferUtility(plant, receiver, "electricity", 30, out var sent), "Export electricity");
    Near(sent, 2.5m, "30 kW limits five-minute export to 2.5 kWh");
    Check(!world.Businesses.TryTransferUtility(plant, receiver, "electricity", 30, out _), "Shared export cap across buyers/calls");
    Near(receiver.UtilityPurchases, sent, "Utility billing");
    Near(world.Cash(), cash, "Utility purchase conserves money");
});
Scenario("kilograms to tonnes and disposal capacity", world =>
{
    var landfill = world.Make("business.landfill");
    world.Prepare(landfill);
    world.Tick();
    world.CheckIn(landfill);
    world.Tick();
    var source = world.Make("business.bakery");
    world.Businesses.TryFundBusiness(source, 1000);
    world.Inventory.TryAdd("waste.ash", source.Inventory, 10);
    decimal balance = source.PaymentAccount.Balance;
    decimal cash = world.Cash();
    Check(world.Businesses.TryDisposeWaste(source, landfill, "waste.ash", 10), "Disposal");
    Near(landfill.DisposedTonnes, 0.01m, "Ten kilograms is 0.01 tonnes");
    Near(balance - source.PaymentAccount.Balance, 0.8m, "80 per tonne is 0.8 for 10 kg");
    Near(world.Cash(), cash, "Disposal conserves money");
    Near(landfill.Inventory.GetQuantity("waste.ash"), 10, "Disposed stock preserved");
});
Scenario("market failure conservation and partial cheapest-first procurement", world =>
{
    var cheap = world.Make("business.bakery");
    world.Prepare(cheap);
    var expensive = world.Make("business.bakery");
    world.Prepare(expensive);
    world.Tick();
    world.CheckIn(cheap);
    world.CheckIn(expensive);
    world.Tick(2);
    world.Inventory.TryAdd("food.bread", cheap.Inventory, 4, 10);
    world.Inventory.TryAdd("food.bread", expensive.Inventory, 4, 60);
    var customer = world.Citizen();
    var inventory = world.Stock(customer);
    var account = world.Account(customer);
    world.Finance.SetBalance(account, 0);
    decimal quantity = cheap.Inventory.GetQuantity("food.bread");
    decimal cash = world.Cash();
    Check(world.Market.TryBuy(inventory, account, "food.bread", 1) == IMarketService.BuyStatus.INSUFFICIENT_FUNDS, "Insufficient funds result");
    Near(cheap.Inventory.GetQuantity("food.bread"), quantity, "Failed purchase preserves seller");
    Near(inventory.GetQuantity("food.bread"), 0, "Failed purchase preserves buyer");
    Near(world.Cash(), cash, "Failed purchase preserves money");
    world.Finance.SetBalance(account, 100);
    inventory.Capacity = 1;
    Check(world.Market.TryBuy(inventory, account, "food.bread", 2) == IMarketService.BuyStatus.INVENTORY_FULL, "Full inventory");
    inventory.Capacity = null;
    var result = world.Market.TryBuyUpTo(inventory, account, "food.bread", 6);
    Near(result.Quantity, 6, "Partial orders combine suppliers");
    Near(result.TotalPrice, 91, "Cheapest four at 13, remaining two at cost-markup 19.5");
    Near(cheap.Inventory.GetQuantity("food.bread"), 0, "Cheapest used first");
    Near(account.Balance, 9, "No overspend");
    Check(!world.Market.GetOffers("food.bread", true).Any(o => o.Seller.Type.Id == "business.supermarket"), "Retail-only suppliers excluded from wholesale");
    Check(!world.Finance.TransferBalance(account, cheap.PaymentAccount, -1), "Negative transfer rejected");
    Check(!world.Finance.RemoveBalance(account, -1), "Negative withdrawal rejected");
    var overflow = world.NewAccount(decimal.MaxValue);
    decimal before = account.Balance;
    Check(!world.Finance.TransferBalance(account, overflow, 1), "Overflow transfer rejected atomically");
    Near(account.Balance, before, "Overflow must not debit sender");
});
Scenario("automatic stock policy and reservation-aware replenishment", world =>
{
    var mill = world.Make("business.flour_mill");
    world.Prepare(mill);
    var bakery = world.Make("business.bakery");
    world.Prepare(bakery);
    world.Inventory.TryAdd("material.flour", mill.Inventory, 50, 100);
    world.Tick();
    world.CheckIn(mill);
    world.CheckIn(bakery);
    world.Tick(2);
    Check(bakery.Inventory.GetQuantity("material.flour") + bakery.Inventory.GetReservedQuantity("material.flour") > 0, "Stock policy purchases available flour");
    Check(bakery.StockPurchases > 0 && mill.SalesRevenue > 0, "Procurement records both sides");
}, procurement: true);
Scenario("bankruptcy grace, debt retention, shutdown, and asset distribution", world =>
{
    var business = world.Make("business.bakery");
    world.Prepare(business, 0, 0);
    world.Finance.SetBalance(business.PaymentAccount, 0);
    world.Tick();
    Check(business.State == BusinessState.Suspended && business.UnpaidOperatingCosts > 0, "Unpaid bill suspends");
    decimal debt = business.UnpaidOperatingCosts;
    world.Jump(288UL * 8);
    Check(business.State == BusinessState.Bankrupt, "Insolvency grace expires");
    Near(business.UnpaidOperatingCosts, debt, "Bankruptcy retains debt");
    Check(!world.Businesses.TryActivateBusiness(business), "Bankrupt cannot reactivate");
    world.Businesses.TryFundBusiness(business, debt + 100);
    Check(world.Businesses.TryPayOperatingCosts(business, business.OperatingCostRecipient!, debt), "Terminal business can settle liabilities");
    bool liquidated = false;
    world.Do(commands => liquidated = world.Businesses.TryLiquidateBusiness(commands, business));
    Check(liquidated && !world.Store.HasComponent<BusinessComponent>(business.Property.Parent), "Liquidation frees premises");
    Near(world.Stock(business.Owner).GetQuantity("equipment.oven"), 1, "Equipment distributed to owner");
    var closed = world.Make("business.bakery");
    world.Prepare(closed);
    Check(world.Businesses.TryCloseBusiness(closed), "Permanent close");
    world.Tick(2);
    Check(closed.State == BusinessState.ShutDown
        && !world.Businesses.TryActivateBusiness(closed), "Hours cannot restart closed business");
});
Scenario("payroll failure, qualifications, recovery, and no phantom work", world =>
{
    var business = world.Make("business.bakery");
    world.Businesses.TryFundBusiness(business, 1000000);
    var unqualified = world.Citizen();
    Check(!world.Businesses.TryHire(business, unqualified, "baker", out _), "Unqualified applicant rejected");
    // Prepare a second business to exercise actual payroll.
    var staffed = world.Make("business.bakery");
    world.Prepare(staffed);
    world.Tick();
    world.CheckIn(staffed);
    decimal before = staffed.Employees.Sum(contract => contract.PaymentAccount.Balance);
    world.Finance.SetBalance(staffed.PaymentAccount, 1);
    world.Tick();
    Check(staffed.State == BusinessState.Suspended
        && staffed.UnpaidWages > 1, "Unfunded payroll creates wage debt and suspends");
    Near(staffed.Employees.Sum(contract => contract.PaymentAccount.Balance), before, "Payroll must not pay only favored employees");
    decimal owed = staffed.UnpaidWages;
    world.Tick(3);
    Near(staffed.UnpaidWages, owed, "Suspension must not invent attendance");
    Check(world.Businesses.TryFundBusiness(staffed, 10000), "Owner recovery funding");
    Check(world.Businesses.TryPayWages(staffed), "Pay wage arrears");
    Check(world.Businesses.TryPayOperatingCosts(staffed, staffed.OperatingCostRecipient!, staffed.UnpaidOperatingCosts), "Pay bill arrears");
    Check(world.Businesses.TryActivateBusiness(staffed), "Resume after settlement");
    Check(staffed.Employees.All(contract => !contract.IsCheckedIn), "Resuming cannot invent check-ins");
    var employee = staffed.Employees[0];
    Check(world.Businesses.TryDismiss(staffed, employee.Employee), "Dismiss contract");
    Check(!world.Businesses.TryCheckIn(staffed, employee.Employee), "Dismissed workers cannot attend");
});
Scenario("utility shortage and demand-limited production", world =>
{
    var business = world.Make("business.bakery");
    world.Prepare(business, 0, 0);
    world.Inventory.TryAdd("material.flour", business.Inventory, 20, 100);
    business.Type.ProductionTargets["food.bread"] = 4;
    world.Tick();
    world.CheckIn(business);
    world.Tick();
    Check(business.Issue == OperationalIssue.MissingUtilities && business.ActiveBatch == null, "No power/water means no production");
    Near(business.Inventory.GetQuantity("material.flour"), 20, "Failed start must not reserve inputs");
    world.Businesses.TryDepositUtilities(business, 100, 1000);
    world.Tick(12);
    Near(business.Inventory.GetQuantity("food.bread"), 4, "Production stops at target");
    Check(business.ActiveBatch == null, "No overproduction while target met");
    world.Tick(3);
    Near(business.Inventory.GetQuantity("food.bread"), 4, "Output stock stays bounded");
    var customer = world.Citizen();
    Check(world.Market.TryBuy(world.Stock(customer), world.Account(customer), "food.bread", 4) == IMarketService.BuyStatus.SUCCESS, "Demand consumes stock");
    world.Tick();
    Check(business.ActiveBatch != null, "Production resumes after demand");
});
Scenario("water works, utility procurement, and exact mass disposal", world =>
{
    var supplier = world.Make("business.water_works");
    world.Prepare(supplier, 100, 0);
    var bakery = world.Make("business.bakery");
    world.Prepare(bakery, 100, 0);
    world.Tick();
    world.CheckIn(supplier);
    world.CheckIn(bakery);
    world.Tick(15);
    Check(supplier.WaterLitres > 0 && bakery.WaterLitres > 0, "Generated water reaches connected businesses");
    Check(bakery.UtilityPurchases > 0, "Automatic utility orders pay suppliers");
}, procurement: true);
Scenario("liquidation sale, overflow failure, and stock-value conservation", world =>
{
    var business = world.Make("business.bakery");
    world.Prepare(business, 0, 0);
    Check(world.Businesses.TryCloseBusiness(business), "Close for liquidation sale");
    var customer = world.Citizen();
    decimal cash = world.Cash();
    Check(world.Market.TryBuyLiquidationStock(business, world.Stock(customer), world.Account(customer), "equipment.oven", 1, 75) == IMarketService.BuyStatus.SUCCESS, "Terminal business can sell equipment explicitly");
    Near(world.Cash(), cash, "Liquidation sale conserves money");
    Near(business.CostOfGoodsSold, 100, "Liquidation recognizes equipment basis");
    var seller = world.Make("business.bakery");
    world.Prepare(seller);
    world.Tick();
    world.CheckIn(seller);
    world.Tick(2);
    world.Inventory.TryAdd("food.bread", seller.Inventory, 1);
    var stock = world.Stock(customer);
    world.Inventory.TryAdd("food.bread", stock, 1, decimal.MaxValue);
    decimal balance = world.Account(customer).Balance;
    bool overflow = false;
    try
    {
        world.Market.TryBuy(stock, world.Account(customer), "food.bread", 1);
    }
    catch (OverflowException)
    {
        overflow = true;
    }

    Check(overflow, "Unrepresentable inventory value fails before committing");
    Near(world.Account(customer).Balance, balance, "Accounting overflow must not debit money");
    Near(seller.Inventory.GetQuantity("food.bread"), 1, "Accounting overflow must not remove seller stock");
});
Scenario("host destination tick is authoritative during callbacks", world =>
{
    var business = world.Make("business.bakery");
    world.Prepare(business);
    var employee = business.Employees[0];
    TimeSpan expected = world.Calendar.GetGameTime(world.Clock.Tick + 1);
    world.AfterBusiness(() => Check(world.Businesses.TryCheckIn(business, employee.Employee), "Check in during host callback"));
    world.TickLikeHost();
    Check(employee.CheckedInAt == expected, "Check-in must use destination tick, not previous committed Clock.Tick");
    Near(employee.MinutesWorked, 0, "Check-in cannot earn retroactive labor");
    world.TickLikeHost();
    Near(employee.MinutesWorked, 5, "Next host callback earns exactly five minutes");
});
Console.WriteLine($"PASS: {checks} business integration checks.");
sealed class TestClock : ISimulationClock
{
    public int TickRate => 30;
    public ulong Tick { get; set; } = 2 * 288 + 96; // Monday 08:00.
    public TimeSpan Delta => TimeSpan.FromSeconds(1.0 / 30);
    public TimeSpan Elapsed => TimeSpan.FromSeconds(Tick / 30.0);
    public bool IsPaused => false;
    public double Speed => 1;
    public bool IsAdvancing => false;
}

class QueuedCommands(int priority = -10) : GameSystem(priority)
{
    public Queue<Action<ICommandBuffer>> Pending { get; } = [];

    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {
        while (Pending.TryDequeue(out var callback))
        {
            callback(commands);
        }
    }
}

sealed class AfterBusinessCommands() : QueuedCommands(1);
sealed class World : IDisposable
{
    public EntityStore Store { get; } = new();
    public TestClock Clock { get; } = new();
    public EventBus Events { get; } = new();
    public CalenderService Calendar { get; } = new();
    public VaultService Finance { get; }
    public InventoryService Inventory { get; }
    public BusinessService Businesses { get; }
    public MarketService Market { get; }
    public IItemService Items { get; }
    public VaultComponent Vault { get; }

    private readonly CitizenService _citizens;
    private readonly PropertyService _properties;
    private readonly SystemScheduler _scheduler;
    private readonly QueuedCommands _queue = new();
    private readonly AfterBusinessCommands _after = new();
    public World(bool procurement)
    {
        var registry = new ComponentRegistry();
        ComponentTypes.Initialize(registry);
        foreach (var assembly in new[]
        {
            typeof(BusinessComponent).Assembly,
            typeof(CitizenComponent).Assembly,
            typeof(MindComponent).Assembly,
            typeof(PropertyComponent).Assembly,
            typeof(InventoryComponent).Assembly,
            typeof(VaultComponent).Assembly,
            typeof(NeedsComponent).Assembly
        }.Distinct())
        {
            registry.RegisterFromAssembly(assembly);
        }

        var services = new ServiceRegistry();
        var systems = new SystemRegistry();
        services.Register<INeedsService>(new NeedsService());
        var context = new ModContext(Store, registry, systems, Clock, Events, services, new EventBus(), new SimulationControls(new SimulationClock(), () =>
        {
        }));
        var output = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            new CitizenSimulator.Items.Items().OnLoad(context);
        }
        finally
        {
            Console.SetOut(output);
        }

        Items = services.Get<IItemService>();
        BusinessCatalog.Load(Items);
        Finance = new(Store, Events);
        Vault = Store.AddComponent(Store.CreateEntity(), new VaultComponent());
        Inventory = new(Store, Events, services.Get<IConsumptionService>(), Items);
        Businesses = new(Store, Events, Finance, Inventory, Calendar, Clock, Items);
        Market = new(Store, Events, Finance, Inventory, Businesses, Items);
        _citizens = new(Store, Events);
        _properties = new(Store, Events);
        systems.Register(_queue);
        systems.Register(new BusinessSystem(Calendar, Businesses));
        systems.Register(_after);
        if (procurement)
            systems.Register(new BusinessRestockSystem(Market, Businesses, Items));

        _scheduler = new(systems, Events);
        _scheduler.Start(Store);
    }

    public BankAccountComponent NewAccount(decimal balance)
    {
        Finance.AddBankAccount(Vault, out var account);
        Finance.SetBalance(account, balance);
        return account;
    }

    public CitizenComponent Citizen()
    {
        var entity = Store.CreateEntity();
        _citizens.CreateCitizen(entity, "Citizen " + entity.Id, 30, out var citizen);
        Store.AddComponent(entity, NewAccount(10000000));
        Store.AddComponent(entity, new InventoryComponent());
        return citizen!;
    }

    public InventoryComponent Stock(CitizenComponent citizen)
    {
        return Store.GetComponent<InventoryComponent>(citizen.Parent)!;
    }

    public BankAccountComponent Account(CitizenComponent citizen)
    {
        return Store.GetComponent<BankAccountComponent>(citizen.Parent)!;
    }

    public PropertyComponent Property(CitizenComponent owner, PropertyComponent.PropertyType use, decimal area)
    {
        var entity = Store.CreateEntity();
        PropertyComponent? result = null;
        Do(commands => _properties.CreateProperty(commands, entity, use, 10, area, 1000, owner, out result));
        return result!;
    }

    public BusinessComponent Make(string typeId)
    {
        var type = Businesses.GetBusinessTypes().Single(t => t.Id == typeId);
        var owner = Citizen();
        var property = Property(owner, Enum.Parse<PropertyComponent.PropertyType>(type.Premises.AllowedUses[0], true), type.Premises.MinimumAreaM2);
        BusinessComponent? business = null;
        Do(commands =>
        {
            if (!Businesses.CreateBusiness(commands, typeId, type.Name, owner, property, out business))
                throw new Exception("Cannot create fixture " + typeId);
        });
        return business!;
    }

    public void Prepare(BusinessComponent business, decimal electricity = 1000, decimal water = 100000)
    {
        if (!Businesses.TryFundBusiness(business, 1000000))
            throw new Exception("Funding fixture");

        foreach (var equipmentRequirement in business.Type.Equipment)
        {
            if (!Inventory.TryAdd(equipmentRequirement.EquipmentId, business.Inventory, equipmentRequirement.Count, equipmentRequirement.Count * 100))
                throw new Exception("Equipment fixture");
        }

        Businesses.TrySetOperatingCostRecipient(business, NewAccount(0));
        Businesses.TryDepositUtilities(business, electricity, water);
        Businesses.TryConnectGrid(business);
        foreach (var role in business.Type.Workforce)
        {
            for (int i = 0; i < role.DesiredCount; i++)
            {
                var worker = Citizen();
                Store.AddComponent(worker.Parent, new WorkerProfileComponent { Skills = new() { [role.Role] = role.MinimumSkill } });
                if (!Businesses.TryHire(business, worker, role.Role, out _))
                    throw new Exception("Hiring fixture " + role.Role);
            }
        }

        if (!Businesses.TryActivateBusiness(business))
            throw new Exception("Activation fixture " + business.Type.Id);
    }

    public void CheckIn(BusinessComponent business)
    {
        foreach (var contract in business.Employees.Where(contract => contract.IsEmployed))
        {
            if (!Businesses.TryCheckIn(business, contract.Employee))
                throw new Exception("Check-in fixture");
        }
    }

    public decimal Cash()
    {
        return Vault.Accounts.Sum(a => a.Balance);
    }

    public void Do(Action<ICommandBuffer> action)
    {
        _queue.Pending.Enqueue(action);
        Tick();
    }

    public void AfterBusiness(Action action)
    {
        _after.Pending.Enqueue(_ => action());
    }

    public void TickLikeHost()
    {
        ulong destination = Clock.Tick + 1;
        _scheduler.Update(new(destination, Clock.Delta, TimeSpan.FromSeconds(destination / 30.0)));
        Clock.Tick = destination;
    }

    public void Tick(int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            Clock.Tick++;
            _scheduler.Update(new(Clock.Tick, Clock.Delta, Clock.Elapsed));
        }
    }

    public void Jump(ulong ticks)
    {
        Clock.Tick += ticks;
        _scheduler.Update(new(Clock.Tick, Clock.Delta, Clock.Elapsed));
    }

    public void Dispose()
    {
        _scheduler.Stop();
    }
}
