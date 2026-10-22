using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Services;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Housing.Components;
using CitizenSimulator.Housing.Models;
using CitizenSimulator.Property.Components;

namespace CitizenSimulator.Housing.Services.Abstractions;

public interface IHousingService : IHomeAccessService
{
    bool CreateHousing(ICommandBuffer commands, PropertyComponent property, uint capacity,
        decimal storageCapacity, bool beds, bool safeWater, bool kitchen, out HouseComponent? house);
    IReadOnlyList<HouseComponent> FindHomes(CitizenComponent citizen);
    IReadOnlyList<PropertySaleListing> FindHomePurchases(CitizenComponent citizen);
    Residence? GetResidence(CitizenComponent citizen);
    bool TryAllocate(CitizenComponent citizen);
    bool TryMoveIn(CitizenComponent citizen, HouseComponent house, Guid? rentalListingId = null);
    bool TryMoveOut(CitizenComponent citizen);
    RentalListing? ListRental(CitizenComponent owner, HouseComponent house, decimal dailyRent, TimeSpan arrearsGrace);
    bool CancelRental(CitizenComponent owner, Guid listingId);
    IReadOnlyList<RentalListing> GetRentalListings();
    IReadOnlyList<Lease> GetLeases(CitizenComponent citizen);
    bool TryCollectRent(Lease lease);
    PropertySaleListing? ListSale(CitizenComponent owner, PropertyComponent property, decimal price);
    bool CancelSale(CitizenComponent owner, Guid listingId);
    IReadOnlyList<PropertySaleListing> GetSaleListings();
    bool TryBuyProperty(CitizenComponent buyer, Guid listingId, decimal expectedPrice);
    bool TryBuyHome(CitizenComponent buyer, Guid listingId, decimal expectedPrice);
    bool TryRepair(CitizenComponent owner, PropertyComponent property, CitizenComponent contractor,
        double conditionPoints, decimal expectedCost);
    IReadOnlyList<HomeStorage> GetStorage(CitizenComponent citizen);
    bool TryStore(CitizenComponent citizen, string itemId, decimal quantity);
    bool TryRetrieve(CitizenComponent citizen, HomeStorage storage, string itemId, decimal quantity);
    void Advance(ulong tick);
}
