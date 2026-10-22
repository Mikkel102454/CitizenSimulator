using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Housing.Components;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Property.Components;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Housing.Models;

public sealed record RentalListing(Guid Id, HouseComponent House, CitizenComponent Landlord,
    decimal DailyRent, TimeSpan ArrearsGrace);

public sealed record PropertySaleListing(Guid Id, PropertyComponent Property,
    CitizenComponent Seller, decimal Price);

public sealed class Lease
{
    public Guid Id { get; } = Guid.NewGuid();
    public required CitizenComponent Tenant { get; init; }
    public required CitizenComponent Landlord { get; init; }
    public required HouseComponent House { get; init; }
    public required decimal DailyRent { get; init; }
    public required TimeSpan ArrearsGrace { get; init; }
    public TimeSpan StartedAt { get; internal set; }
    public TimeSpan? EndedAt { get; internal set; }
    public decimal RentAccrued { get; internal set; }
    public decimal RentPaid { get; internal set; }
    public decimal Arrears => RentAccrued - RentPaid;
    public TimeSpan? UnpaidSince { get; internal set; }
    internal TimeSpan AccountedUntil { get; set; }
    internal BankAccountComponent TenantAccount { get; init; } = null!;
    internal BankAccountComponent LandlordAccount { get; init; } = null!;
}

public sealed class Residence
{
    public required CitizenComponent Citizen { get; init; }
    public required HouseComponent House { get; init; }
    public Lease? Lease { get; internal set; }
    public TimeSpan MovedInAt { get; internal set; }
}

// Belongings stay associated with their resident and location after move-out or death.
public sealed class HomeStorage
{
    public required CitizenComponent Owner { get; init; }
    public required HouseComponent House { get; init; }
    internal InventoryComponent Inventory { get; } = new();
    public IReadOnlyDictionary<string, decimal> Items =>
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, decimal>(Inventory.Inventory);
    public decimal UsedCapacity => Inventory.UsedCapacity;
}
