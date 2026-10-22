using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Housing.Components;
using CitizenSimulator.Housing.Services;
using CitizenSimulator.Property.Components;
using CitizenSimulator.Utilities.Models;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Utilities.Services;

public sealed partial class UtilityService
{
    private readonly Dictionary<EntityRef, HomeUtilityConnection> _homes = [];
    private readonly HashSet<EntityRef> _disconnectedHomes = [];

    public bool ConnectHome(EntityRef entity)
    {
        if (!IsUsableHome(entity))
            return false;

        _homes.TryAdd(entity, new HomeUtilityConnection { House = entity, AccountedUntil = Now });
        if (_disconnectedHomes.Contains(entity))
            _homes[entity].AccountedUntil = Now;

        _disconnectedHomes.Remove(entity);
        _homes[entity].Connected = true;
        return true;
    }

    public bool DisconnectHome(EntityRef entity)
    {
        if (!_homes.ContainsKey(entity))
            return false;

        // Keep the paid buffer and meter history, but remove access while disconnected.
        _disconnectedHomes.Add(entity);
        _homes[entity].Connected = false;
        return true;
    }

    public HomeUtilityConnection? GetHomeConnection(EntityRef entity) => _homes.GetValueOrDefault(entity);
    public bool HasWater(EntityRef entity) => IsUsableHome(entity)
        && store.GetComponent<HouseComponent>(entity)!.HasSafeWater && !_disconnectedHomes.Contains(entity)
        && _homes.GetValueOrDefault(entity)?.WaterLitres > 0;
    public bool HasElectricity(EntityRef entity) => IsUsableHome(entity) && !_disconnectedHomes.Contains(entity)
        && _homes.GetValueOrDefault(entity)?.ElectricityKwh > 0;

    public bool TryConsumeDrinkingWater(EntityRef citizen, decimal litres)
    {
        HomeUtilityConnection? connection = GetResidentMeter(citizen);
        if (litres <= 0 || connection == null || connection.WaterLitres < litres)
            return false;

        ConsumeDrinkingWater(citizen, litres);
        return true;
    }

    public decimal ConsumeDrinkingWater(EntityRef citizen, decimal requestedLitres)
    {
        HomeUtilityConnection? connection = GetResidentMeter(citizen);
        if (requestedLitres <= 0 || connection == null)
            return 0;

        decimal consumed = Math.Min(requestedLitres, connection.WaterLitres);
        connection.WaterLitres -= consumed;
        connection.WaterConsumed += consumed;
        return consumed;
    }

    private HomeUtilityConnection? GetResidentMeter(EntityRef citizen)
    {
        if (!store.IsAlive(citizen) || store.GetComponent<CitizenComponent>(citizen) is not { Alive: true } person)
            return null;

        return _homes.Values.FirstOrDefault(connection => HasWater(connection.House)
            && store.GetComponent<HouseComponent>(connection.House)!.Occupants.Contains(person));
    }

    public void SupplyHomes()
    {
        if (!AccountsReady)
            return;

        RefreshLimits();
        foreach (HomeUtilityConnection connection in _homes.Values.OrderBy(connection => connection.House.Id))
        {
            if (!IsUsableHome(connection.House) || _disconnectedHomes.Contains(connection.House))
                continue;

            HouseComponent house = store.GetComponent<HouseComponent>(connection.House)!;
            var owner = house.Property.Owner!;
            BankAccountComponent? account = store.GetComponent<BankAccountComponent>(owner.Parent);
            if (account == null || !IsAttachedAccount(account))
                continue;

            decimal residents = house.Occupants.Count(citizen => citizen.Alive && store.IsAlive(citizen.Parent));
            decimal days = Configuration.Households.BufferHours / 24;
            SupplyHomeUtility(connection, account, "electricity", residents * days * Configuration.Households.ElectricityKwhPerResidentPerDay);
            if (house.HasSafeWater)
                SupplyHomeUtility(connection, account, "water", residents * days * Configuration.Households.WaterLitresPerResidentPerDay);
        }
    }

    private void AdvanceHomes()
    {
        foreach (EntityRef entity in _homes.Keys.ToArray())
        {
            if (!store.IsAlive(entity) || !store.HasComponent<HouseComponent>(entity))
            {
                _homes.Remove(entity);
                _disconnectedHomes.Remove(entity);
            }
        }

        foreach (EntityRef entity in store.Query(HouseComponent.GetComponentType()))
        {
            if (Configuration.Households.AutoConnect && !_disconnectedHomes.Contains(entity) && !_homes.ContainsKey(entity))
                ConnectHome(entity);
        }

        foreach (HomeUtilityConnection connection in _homes.Values)
        {
            TimeSpan from = connection.AccountedUntil ?? Now;
            connection.AccountedUntil = Now;
            if (!IsUsableHome(connection.House) || _disconnectedHomes.Contains(connection.House) || Now <= from)
                continue;

            decimal residents = store.GetComponent<HouseComponent>(connection.House)!.Occupants.Count;
            decimal days = (Now - from).Ticks / (decimal)TimeSpan.TicksPerDay;
            decimal electricity = residents * days * Configuration.Households.ElectricityKwhPerResidentPerDay;
            decimal water = residents * days * Configuration.Households.WaterLitresPerResidentPerDay;
            connection.UnservedElectricityKwh += Math.Max(0, electricity - connection.ElectricityKwh);
            connection.UnservedWaterLitres += Math.Max(0, water - connection.WaterLitres);
            decimal consumedEnergy = Math.Min(electricity, connection.ElectricityKwh);
            decimal consumedWater = Math.Min(water, connection.WaterLitres);
            connection.ElectricityKwh -= consumedEnergy;
            connection.WaterLitres -= consumedWater;
            connection.ElectricityConsumed += consumedEnergy;
            connection.WaterConsumed += consumedWater;
        }
    }

    private void SupplyHomeUtility(HomeUtilityConnection connection, BankAccountComponent account, string utility, decimal target)
    {
        if (connection.DeliveryTime != Now)
        {
            connection.DeliveryTime = Now;
            connection.ElectricityDelivered = 0;
            connection.WaterDelivered = 0;
        }

        bool electricity = utility == "electricity";
        decimal buffer = electricity ? connection.ElectricityKwh : connection.WaterLitres;
        decimal requested = Math.Max(0, target - buffer);
        decimal rate = electricity ? Configuration.Households.ElectricityKw : Configuration.Households.WaterLitresPerHour;
        decimal used = electricity ? connection.ElectricityDelivered : connection.WaterDelivered;
        decimal remaining = Math.Min(requested, Math.Max(0, PerTick(rate) - used));
        decimal delivered = 0;
        decimal cost = 0;
        SupplyStatistics statistics = Statistics(utility);
        statistics.Requested += requested;
        foreach (var supplier in GetBusinesses().Where(supplier => supplier.Type.GridSupplyPolicy?.Utility == utility && CanSupply(supplier))
                     .OrderBy(supplier => supplier.Type.GridSupplyPolicy!.UnitPrice).ThenBy(supplier => supplier.Parent.Id))
        {
            decimal exportable = Math.Max(0, Buffer(supplier, utility) - UtilityTarget(supplier, utility));
            decimal amount = Math.Min(remaining - delivered, exportable);
            if (amount > 0 && businesses.TrySellUtility(supplier, account, utility, amount, out decimal received, out decimal price))
            {
                delivered += received;
                cost += price;
                statistics.LocalDelivered += received;
            }

            if (delivered >= remaining)
                break;
        }

        decimal imported = ImportHomeUtility(account, utility, remaining - delivered, out decimal importCost);
        delivered += imported;
        cost += importCost;
        connection.Purchases += cost;
        if (electricity)
        {
            connection.ElectricityKwh += delivered;
            connection.ElectricityDelivered += delivered;
        }
        else
        {
            connection.WaterLitres += delivered;
            connection.WaterDelivered += delivered;
        }

        _shortages[(connection.House, utility, true)] = requested - delivered;
    }

    private decimal ImportHomeUtility(BankAccountComponent account, string utility, decimal requested, out decimal cost)
    {
        cost = 0;
        if (requested <= 0 || !Configuration.Utilities.TryGetValue(utility, out var policy) || !policy.Enabled)
            return 0;

        decimal available = Math.Max(0, PerTick(policy.MaxPerHour) - _utilityImportsThisTick.GetValueOrDefault(utility));
        decimal amount = Math.Min(requested, available);
        bool affordableWithoutDivision = policy.UnitPrice is > 0 and < 1 && amount * policy.UnitPrice <= account.Balance;
        if (policy.UnitPrice > 0 && !affordableWithoutDivision)
            amount = Math.Min(amount, account.Balance / policy.UnitPrice);

        decimal price = amount * policy.UnitPrice;
        if (amount <= 0 || (price > 0 && !vault.TransferBalance(account, WorldAccount!, price)))
            return 0;

        _utilityImportsThisTick[utility] = _utilityImportsThisTick.GetValueOrDefault(utility) + amount;
        Statistics(utility).Imported += amount;
        Statistics(utility).ImportCost += price;
        cost = price;
        return amount;
    }

    private bool IsUsableHome(EntityRef entity)
    {
        if (!store.IsAlive(entity) || store.GetComponent<HouseComponent>(entity) is not { } house)
            return false;

        var property = house.Property;
        var owner = property.Owner;
        return !property.Destroyed && property.Condition >= HousingService.MinimumHabitableCondition && owner is { Alive: true }
            && store.IsAlive(owner.Parent) && ReferenceEquals(store.GetComponent<CitizenComponent>(owner.Parent), owner)
            && store.IsAlive(property.Parent)
            && ReferenceEquals(store.GetComponent<PropertyComponent>(property.Parent), property);
    }
}
