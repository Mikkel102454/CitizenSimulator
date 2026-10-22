using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Housing.Models;
using CitizenSimulator.Property.Components;

namespace CitizenSimulator.Housing.Services;

public sealed partial class HousingService
{
    public PropertySaleListing? ListSale(CitizenComponent owner, PropertyComponent property, decimal price)
    {
        if (!IsRegistered(property) || property.Destroyed || !IsLiving(owner)
            || !ReferenceEquals(property.Owner, owner) || price <= 0 || GetAccount(owner) == null)
            return null;

        foreach (PropertySaleListing previous in _sales.Values.Where(sale => ReferenceEquals(sale.Property, property)).ToArray())
        {
            _sales.Remove(previous.Id);
        }

        PropertySaleListing listing = new(Guid.NewGuid(), property, owner, price);
        _sales.Add(listing.Id, listing);
        return listing;
    }

    public bool CancelSale(CitizenComponent owner, Guid listingId)
    {
        return IsLiving(owner) && _sales.TryGetValue(listingId, out PropertySaleListing? sale)
            && ReferenceEquals(sale.Seller, owner) && ReferenceEquals(sale.Property.Owner, owner)
            && _sales.Remove(listingId);
    }

    public IReadOnlyList<PropertySaleListing> GetSaleListings() => _sales.Values
        .Where(IsSaleValid).OrderBy(sale => sale.Price).ThenBy(sale => sale.Property.Parent.Id).ToArray();

    public bool TryBuyProperty(CitizenComponent buyer, Guid listingId, decimal expectedPrice)
    {
        if (!_sales.TryGetValue(listingId, out PropertySaleListing? sale) || !IsSaleValid(sale)
            || expectedPrice != sale.Price || ReferenceEquals(buyer, sale.Seller)
            || GetAccount(buyer) is not { } buyerAccount || GetAccount(sale.Seller) is not { } sellerAccount)
            return false;

        if (!vault.TransferBalance(buyerAccount, sellerAccount, sale.Price))
            return false;

        if (!properties.TransferOwnership(sale.Property, buyer))
        {
            if (!vault.TransferBalance(sellerAccount, buyerAccount, sale.Price))
                throw new InvalidOperationException("Could not reverse a failed property purchase.");

            return false;
        }

        _sales.Remove(listingId);
        foreach (RentalListing rental in _rentals.Values.Where(rental => ReferenceEquals(rental.House.Property, sale.Property)).ToArray())
        {
            _rentals.Remove(rental.Id);
        }

        foreach (Residence residence in _residences.Values.Where(residence => ReferenceEquals(residence.House.Property, sale.Property)).ToArray())
        {
            if (residence.Lease != null)
                TransferLease(residence);
            else if (!ReferenceEquals(residence.Citizen, buyer))
                EndResidence(residence);
        }

        return true;
    }

    public bool TryRepair(CitizenComponent owner, PropertyComponent property, CitizenComponent contractor,
        double conditionPoints, decimal expectedCost)
    {
        if (!IsRegistered(property) || property.Destroyed || !ReferenceEquals(property.Owner, owner)
            || !double.IsFinite(property.Condition) || !double.IsFinite(conditionPoints)
            || conditionPoints <= 0 || conditionPoints > 100 || property.Condition + conditionPoints > 100
            || GetAccount(owner) is not { } ownerAccount || GetAccount(contractor) is not { } contractorAccount)
            return false;

        decimal cost = (decimal)conditionPoints * RepairCostPerPoint;
        if (cost <= 0 || expectedCost != cost || !vault.TransferBalance(ownerAccount, contractorAccount, cost))
            return false;

        if (!properties.RepairProperty(property, conditionPoints))
        {
            if (!vault.TransferBalance(contractorAccount, ownerAccount, cost))
                throw new InvalidOperationException("Could not reverse a failed property repair.");

            return false;
        }
        return true;
    }

    private bool IsSaleValid(PropertySaleListing sale) => IsRegistered(sale.Property) && !sale.Property.Destroyed
        && IsLiving(sale.Seller) && ReferenceEquals(sale.Property.Owner, sale.Seller) && GetAccount(sale.Seller) != null;
}
