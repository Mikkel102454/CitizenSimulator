using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Services;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Calender.Services.Abstractions;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.CitizenMind.Components;
using CitizenSimulator.Housing.Components;
using CitizenSimulator.Housing.Models;
using CitizenSimulator.Housing.Services.Abstractions;
using CitizenSimulator.Inventory.Services.Abstractions;
using CitizenSimulator.Property.Components;
using CitizenSimulator.Property.Services.Abstractions;
using CitizenSimulator.Vault.Components;
using CitizenSimulator.Vault.Services.Abstractions;

namespace CitizenSimulator.Housing.Services;

public sealed partial class HousingService(IEntityStore store, IPropertyService properties,
    IVaultService vault, IInventoryService inventories, ICalenderService calendar,
    ISimulationClock clock, IServiceRegistry? services = null) : IHousingService
{
    public const double MinimumHabitableCondition = 20;
    public const decimal RepairCostPerPoint = 10;

    private readonly Dictionary<EntityRef, Residence> _residences = [];
    private readonly Dictionary<Guid, RentalListing> _rentals = [];
    private readonly Dictionary<Guid, PropertySaleListing> _sales = [];
    private readonly List<Lease> _leases = [];
    private readonly List<HomeStorage> _storage = [];
    private readonly HashSet<EntityRef> _pendingHouses = [];
    private TimeSpan _observedTime;

    private TimeSpan Now => TimeSpan.FromTicks(Math.Max(_observedTime.Ticks, calendar.GetGameTime(clock.Tick).Ticks));

    public bool CreateHousing(ICommandBuffer commands, PropertyComponent property, uint capacity,
        decimal storageCapacity, bool beds, bool safeWater, bool kitchen, out HouseComponent? house)
    {
        house = null;
        if (!IsUsable(property) || capacity == 0 || capacity > property.Capacity || storageCapacity < 0
            || property.Type is not (PropertyComponent.PropertyType.Residential or PropertyComponent.PropertyType.Mixed)
            || store.HasComponent<HouseComponent>(property.Parent) || _pendingHouses.Contains(property.Parent))
            return false;

        house = commands.AddComponent(property.Parent, new HouseComponent
        {
            Property = property,
            MaxOccupants = capacity,
            StorageCapacity = storageCapacity,
            HasBeds = beds,
            HasSafeWater = safeWater,
            HasKitchen = kitchen
        });
        _pendingHouses.Add(property.Parent);
        return true;
    }

    public Residence? GetResidence(CitizenComponent citizen)
    {
        if (!IsLiving(citizen))
            return null;

        return _residences.GetValueOrDefault(citizen.Parent);
    }

    public IReadOnlyList<HouseComponent> FindHomes(CitizenComponent citizen)
    {
        if (!IsLiving(citizen))
            return [];

        return store.Query(HouseComponent.GetComponentType())
            .Select(entity => store.GetComponent<HouseComponent>(entity)!)
            .Where(house => CanMoveIn(citizen, house, GetRental(house)))
            .OrderBy(house => ReferenceEquals(house.Property.Owner, citizen) ? 0 : 1)
            .ThenBy(house => GetRental(house)?.DailyRent ?? 0)
            .ThenBy(house => house.Parent.Id)
            .ToArray();
    }

    public bool TryAllocate(CitizenComponent citizen)
    {
        if (!IsLiving(citizen))
            return false;

        if (GetResidence(citizen) is { } current && IsUsable(current.House))
            return true;

        foreach (HouseComponent house in FindHomes(citizen))
        {
            if (TryMoveIn(citizen, house, GetRental(house)?.Id))
                return true;
        }

        return false;
    }

    public bool TryMoveIn(CitizenComponent citizen, HouseComponent house, Guid? rentalListingId = null)
    {
        RentalListing? rental = GetRental(house);
        if (!CanMoveIn(citizen, house, rental))
            return false;

        bool ownsHome = ReferenceEquals(house.Property.Owner, citizen);
        if (!ownsHome && rental?.Id != rentalListingId)
            return false;

        if (_residences.TryGetValue(citizen.Parent, out Residence? existing)
            && ReferenceEquals(existing.House, house))
            return false;

        // Rent due at this instant may change affordability before the move commits.
        if (existing?.Lease != null)
        {
            TryCollectRent(existing.Lease);
            if (!CanMoveIn(citizen, house, rental))
                return false;
        }

        // Validate the destination before releasing the current home.
        TryMoveOut(citizen);
        Lease? lease = ownsHome ? null : CreateLease(citizen, rental!);
        Residence residence = new()
        {
            Citizen = citizen,
            House = house,
            Lease = lease,
            MovedInAt = Now
        };
        _residences.Add(citizen.Parent, residence);
        house.AddOccupant(citizen);
        return true;
    }

    public bool TryMoveOut(CitizenComponent citizen)
    {
        if (!_residences.TryGetValue(citizen.Parent, out Residence? residence)
            || !ReferenceEquals(residence.Citizen, citizen))
            return false;

        EndResidence(residence);
        return true;
    }

    public bool CanSleep(EntityRef citizen) => GetAccessibleHome(citizen) is { HasBeds: true };
    public bool CanDrink(EntityRef citizen)
    {
        if (GetAccessibleHome(citizen) is not { HasSafeWater: true } house)
            return false;

        return services == null || !services.TryGet<IHomeUtilityService>(out var utilities) || utilities!.HasWater(house.Parent);
    }

    public bool CanCook(EntityRef citizen)
    {
        if (GetAccessibleHome(citizen) is not { HasKitchen: true, HasSafeWater: true } house || !CanDrink(citizen))
            return false;

        return services == null || !services.TryGet<IHomeUtilityService>(out var utilities) || utilities!.HasElectricity(house.Parent);
    }
    public bool CanStore(EntityRef citizen) => GetAccessibleHome(citizen) is { StorageCapacity: > 0 };

    public void Advance(ulong tick)
    {
        TimeSpan destination = calendar.GetGameTime(tick);
        if (destination < _observedTime)
            return;

        _observedTime = destination;
        _pendingHouses.Clear();
        List<CitizenComponent> displaced = [];

        // Existing debts retain their original creditor, including after sale or death.
        foreach (Lease lease in _leases.ToArray())
        {
            AccrueRent(lease);
            TryCollectRent(lease);
        }

        foreach (Residence residence in _residences.Values.ToArray())
        {
            bool evict = residence.Lease is { EndedAt: null, UnpaidSince: { } since } lease
                && lease.Arrears > 0 && Now - since >= lease.ArrearsGrace;
            if (!IsLiving(residence.Citizen) || !IsUsable(residence.House) || evict
                || !IsLiving(residence.House.Property.Owner))
            {
                EndResidence(residence);
                if (IsLiving(residence.Citizen))
                    displaced.Add(residence.Citizen);
            }
            else if (residence.Lease is { } activeLease
                     && !ReferenceEquals(activeLease.Landlord, residence.House.Property.Owner))
            {
                // Direct property transfers also preserve the resident's agreed rent.
                TransferLease(residence);
            }
            else if (residence.Lease == null && !ReferenceEquals(residence.House.Property.Owner, residence.Citizen))
            {
                EndResidence(residence);
            }
        }

        foreach (PropertyComponent property in store.Query(PropertyComponent.GetComponentType())
                     .Select(entity => store.GetComponent<PropertyComponent>(entity)!))
        {
            if (property.Owner != null && !IsLiving(property.Owner))
                properties.TransferOwnership(property, null);
        }

        foreach (RentalListing rental in _rentals.Values.ToArray())
        {
            if (!IsRentalValid(rental))
                _rentals.Remove(rental.Id);
        }

        foreach (PropertySaleListing sale in _sales.Values.ToArray())
        {
            if (!IsSaleValid(sale))
                _sales.Remove(sale.Id);
        }

        // Give displaced residents first chance before filling the remaining vacancies.
        foreach (CitizenComponent citizen in displaced)
        {
            if (ShouldAllocateAutomatically(citizen))
                TryAllocate(citizen);
        }

        // Allocation retries each tick so homeless citizens can take newly available homes.
        foreach (EntityRef entity in store.Query(CitizenComponent.GetComponentType()).OrderBy(entity => entity.Id))
        {
            CitizenComponent citizen = store.GetComponent<CitizenComponent>(entity)!;
            if (!_residences.ContainsKey(entity) && ShouldAllocateAutomatically(citizen))
                TryAllocate(citizen);
        }
    }

    private bool ShouldAllocateAutomatically(CitizenComponent citizen)
    {
        return store.GetComponent<MindComponent>(citizen.Parent) is not { IsActive: true };
    }

    private void EndResidence(Residence residence)
    {
        if (residence.Lease != null)
        {
            AccrueRent(residence.Lease);
            TryCollectRent(residence.Lease);
            residence.Lease.EndedAt = Now;
        }

        residence.House.RemoveOccupant(residence.Citizen);
        _residences.Remove(residence.Citizen.Parent);
    }

    private bool CanMoveIn(CitizenComponent citizen, HouseComponent house, RentalListing? rental)
    {
        if (!IsLiving(citizen) || !IsUsable(house) || house.Occupants.Count >= house.MaxOccupants)
            return false;

        if (ReferenceEquals(house.Property.Owner, citizen))
            return true;

        if (rental == null || !IsRentalValid(rental) || GetAccount(citizen) is not { } account
            || account.Balance < rental.DailyRent || HasCollectibleRentDebt(citizen))
            return false;

        return true;
    }

    private bool HasCollectibleRentDebt(CitizenComponent citizen)
    {
        // Estate claims are preserved without preventing relocation to a living landlord.
        return _leases.Any(lease => ReferenceEquals(lease.Tenant, citizen)
            && lease.Arrears > 0 && IsLiving(lease.Landlord));
    }

    private HouseComponent? GetAccessibleHome(EntityRef citizen)
    {
        if (!_residences.TryGetValue(citizen, out Residence? residence) || !IsLiving(residence.Citizen)
            || !IsUsable(residence.House) || !IsLiving(residence.House.Property.Owner))
            return null;

        bool ownsHome = ReferenceEquals(residence.House.Property.Owner, residence.Citizen);
        return ownsHome || residence.Lease is { EndedAt: null } ? residence.House : null;
    }

    private bool IsLiving(CitizenComponent? citizen) => citizen is { Alive: true }
        && store.IsAlive(citizen.Parent) && ReferenceEquals(store.GetComponent<CitizenComponent>(citizen.Parent), citizen);

    private bool IsRegistered(PropertyComponent property) => store.IsAlive(property.Parent)
        && ReferenceEquals(store.GetComponent<PropertyComponent>(property.Parent), property);

    private bool IsUsable(PropertyComponent property) => IsRegistered(property) && !property.Destroyed
        && double.IsFinite(property.Condition) && property.Condition >= MinimumHabitableCondition;

    private bool IsUsable(HouseComponent house) => store.IsAlive(house.Parent)
        && ReferenceEquals(store.GetComponent<HouseComponent>(house.Parent), house)
        && house.Property.Parent == house.Parent && IsUsable(house.Property);

    private BankAccountComponent? GetAccount(CitizenComponent citizen)
    {
        if (!IsLiving(citizen))
            return null;

        BankAccountComponent? account = store.GetComponent<BankAccountComponent>(citizen.Parent);
        return account != null && account.Parent == citizen.Parent && account.Vault.Accounts.Contains(account) ? account : null;
    }
}
