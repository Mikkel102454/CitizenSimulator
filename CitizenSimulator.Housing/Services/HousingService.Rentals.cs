using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Housing.Components;
using CitizenSimulator.Housing.Models;

namespace CitizenSimulator.Housing.Services;

public sealed partial class HousingService
{
    public RentalListing? ListRental(CitizenComponent owner, HouseComponent house,
        decimal dailyRent, TimeSpan arrearsGrace)
    {
        if (!IsLiving(owner) || !IsUsable(house) || !ReferenceEquals(house.Property.Owner, owner)
            || dailyRent <= 0 || arrearsGrace < TimeSpan.FromDays(1) || GetAccount(owner) == null)
            return null;

        foreach (RentalListing previous in _rentals.Values.Where(listing => ReferenceEquals(listing.House, house)).ToArray())
        {
            _rentals.Remove(previous.Id);
        }

        RentalListing rental = new(Guid.NewGuid(), house, owner, dailyRent, arrearsGrace);
        _rentals.Add(rental.Id, rental);
        return rental;
    }

    public bool CancelRental(CitizenComponent owner, Guid listingId)
    {
        return IsLiving(owner) && _rentals.TryGetValue(listingId, out RentalListing? rental)
            && ReferenceEquals(rental.Landlord, owner) && ReferenceEquals(rental.House.Property.Owner, owner)
            && _rentals.Remove(listingId);
    }

    public IReadOnlyList<RentalListing> GetRentalListings() => _rentals.Values
        .Where(IsRentalValid).OrderBy(rental => rental.DailyRent).ThenBy(rental => rental.House.Parent.Id).ToArray();

    public IReadOnlyList<Lease> GetLeases(CitizenComponent citizen) => _leases
        .Where(lease => ReferenceEquals(lease.Tenant, citizen) || ReferenceEquals(lease.Landlord, citizen)).ToArray();

    public bool TryCollectRent(Lease lease)
    {
        if (!_leases.Contains(lease))
            return false;

        AccrueRent(lease);
        if (lease.Arrears <= 0)
            return true;

        if (!IsLiving(lease.Tenant) || !IsLiving(lease.Landlord)
            || !ReferenceEquals(GetAccount(lease.Tenant), lease.TenantAccount)
            || !ReferenceEquals(GetAccount(lease.Landlord), lease.LandlordAccount))
            return false;

        decimal payment = Math.Min(lease.Arrears, lease.TenantAccount.Balance);
        if (payment <= 0 || !vault.TransferBalance(lease.TenantAccount, lease.LandlordAccount, payment))
            return false;

        lease.RentPaid += payment;
        if (lease.Arrears == 0)
            lease.UnpaidSince = null;

        return lease.Arrears == 0;
    }

    private Lease CreateLease(CitizenComponent citizen, RentalListing rental)
    {
        Lease lease = new()
        {
            Tenant = citizen,
            Landlord = rental.Landlord,
            House = rental.House,
            DailyRent = rental.DailyRent,
            ArrearsGrace = rental.ArrearsGrace,
            TenantAccount = GetAccount(citizen)!,
            LandlordAccount = GetAccount(rental.Landlord)!,
            StartedAt = Now,
            AccountedUntil = Now
        };
        _leases.Add(lease);
        return lease;
    }

    private void AccrueRent(Lease lease)
    {
        if (lease.EndedAt != null || Now <= lease.AccountedUntil)
            return;

        TimeSpan elapsed = Now - lease.StartedAt;
        decimal days = (decimal)elapsed.Ticks / TimeSpan.TicksPerDay;
        bool exceedsLedger = days > 1 && lease.DailyRent > decimal.MaxValue / days;
        decimal totalRent = exceedsLedger ? decimal.MaxValue : days * lease.DailyRent;
        decimal charge = totalRent - lease.RentAccrued;
        lease.RentAccrued = totalRent;
        if (charge > 0 && lease.UnpaidSince == null)
            lease.UnpaidSince = lease.AccountedUntil;

        lease.AccountedUntil = Now;
    }

    private void TransferLease(Residence residence)
    {
        Lease previous = residence.Lease!;
        AccrueRent(previous);
        TryCollectRent(previous);
        previous.EndedAt = Now;

        CitizenComponent owner = residence.House.Property.Owner!;
        if (ReferenceEquals(owner, residence.Citizen))
        {
            residence.Lease = null;
            return;
        }

        if (GetAccount(owner) == null)
        {
            EndResidence(residence);
            return;
        }

        Lease replacement = new()
        {
            Tenant = previous.Tenant,
            Landlord = owner,
            House = previous.House,
            DailyRent = previous.DailyRent,
            ArrearsGrace = previous.ArrearsGrace,
            TenantAccount = previous.TenantAccount,
            LandlordAccount = GetAccount(owner)!,
            StartedAt = Now,
            AccountedUntil = Now
        };
        _leases.Add(replacement);
        residence.Lease = replacement;
    }

    private RentalListing? GetRental(HouseComponent house) => _rentals.Values
        .FirstOrDefault(rental => ReferenceEquals(rental.House, house) && IsRentalValid(rental));

    private bool IsRentalValid(RentalListing rental) => IsUsable(rental.House) && IsLiving(rental.Landlord)
        && ReferenceEquals(rental.House.Property.Owner, rental.Landlord) && GetAccount(rental.Landlord) != null;
}
