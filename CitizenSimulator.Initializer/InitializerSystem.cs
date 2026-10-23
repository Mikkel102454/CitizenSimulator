using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Services;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Models;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Housing.Components;
using CitizenSimulator.Housing.Services.Abstractions;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Needs.Components;
using CitizenSimulator.Property.Components;
using CitizenSimulator.Utilities.Services.Abstractions;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Initializer;

internal sealed record SeedHome(HouseComponent House, bool ForSale, decimal Price);
internal sealed record SeedBusiness(string TypeId, string Name, PropertyComponent Property);

internal sealed class InitializerSystem(IServiceRegistry services, IReadOnlyList<CitizenComponent> people,
    IReadOnlyList<SeedHome> homes, IReadOnlyList<SeedBusiness> premises) : GameSystem(priority: -6)
{
    private readonly List<BusinessComponent> _businesses = [];
    private readonly HashSet<EntityRef> _activated = [];
    private readonly HashSet<EntityRef> _listed = [];
    private bool _businessesCreated;
    private bool _completed;
    private int _initializationTicks;

    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {
        var businesses = services.Get<IBusinessService>();
        if (!_completed)
        {
            _initializationTicks++;
            var missing = FindMissingPrerequisite();
            if (missing != null)
            {
                Initializer.Require(_initializationTicks < 10, $"Initialization timed out: {missing}");
                return;
            }

            if (!_businessesCreated)
            {
                foreach (var seed in premises)
                {
                    Initializer.Require(businesses.CreateBusiness(commands, seed.TypeId, seed.Name,
                        seed.Property.Owner!, seed.Property, out var business) && business != null,
                        $"Could not create '{seed.Name}' on entity {seed.Property.Parent.Id}.");
                    _businesses.Add(business!);
                }
                _businessesCreated = true;
                return; // Wait for the scheduler to commit the components.
            }

            var utilities = services.Get<IUtilityService>();
            foreach (var business in _businesses)
            {
                Initializer.Require(Store.IsAlive(business.Parent)
                    && ReferenceEquals(Store.GetComponent<BusinessComponent>(business.Parent), business),
                    $"Business '{business.Name}' was not committed.");
                Initializer.Require(businesses.TrySetOperatingCostRecipient(business, utilities.WorldAccount!),
                    $"Could not assign operating-cost recipient for '{business.Name}'.");
            }

            var housing = services.Get<IHousingService>();
            PublishListings(housing);
            _completed = true;
            Console.WriteLine($"[INITIALIZER] Ready: {people.Count} citizens, {housing.GetSaleListings().Count} sales, " +
                $"{housing.GetRentalListings().Count} rentals, {_businesses.Count} businesses, " +
                $"{businesses.GetVacancies().Sum(job => job.AvailablePositions)} vacancies. Citizens choose homes and jobs.");
        }

        PublishListings(services.Get<IHousingService>());

        foreach (var business in _businesses)
        {
            if (!_activated.Contains(business.Parent) && business.State == BusinessState.SettingUp
                && businesses.TryActivateBusiness(business))
            {
                _activated.Add(business.Parent);
                Console.WriteLine($"[INITIALIZER] Activated {business.Name} ({_activated.Count}/{_businesses.Count}).");
            }
        }
    }

    private void PublishListings(IHousingService housing)
    {
        // Small batches fit the planner's 32-candidate limit and reduce contention
        // while 200 citizens independently search for their first home.
        int sales = housing.GetSaleListings().Count;
        int vacantRentals = housing.GetRentalListings().Count(listing =>
            listing.House.Occupants.Count < listing.House.MaxOccupants);
        foreach (var seed in homes)
        {
            if (_listed.Contains(seed.House.Parent)) continue;
            if (seed.ForSale ? sales >= 8 : vacantRentals >= 4) continue;
            bool listed = seed.ForSale
                ? housing.ListSale(seed.House.Property.Owner!, seed.House.Property, seed.Price) != null
                : housing.ListRental(seed.House.Property.Owner!, seed.House, seed.Price, TimeSpan.FromDays(7)) != null;
            Initializer.Require(listed, $"Could not list home on entity {seed.House.Parent.Id}.");
            _listed.Add(seed.House.Parent);
            if (seed.ForSale) sales++;
            else if (seed.House.Occupants.Count < seed.House.MaxOccupants) vacantRentals++;
        }
    }

    private string? FindMissingPrerequisite()
    {
        foreach (var person in people)
        {
            var entity = person.Parent;
            if (!Store.IsAlive(entity)) return $"citizen entity {entity.Id} is not alive.";
            var account = Store.GetComponent<BankAccountComponent>(entity);
            if (account == null || !account.Vault.Accounts.Contains(account)) return $"citizen {entity.Id} has no registered account.";
            if (!Store.HasComponent<WalletComponent>(entity)) return $"citizen {entity.Id} has no wallet.";
            if (!Store.HasComponent<InventoryComponent>(entity)) return $"citizen {entity.Id} has no inventory.";
            if (!Store.HasComponent<NeedsComponent>(entity)) return $"citizen {entity.Id} has no needs component.";
        }
        var world = services.Get<IUtilityService>().WorldAccount;
        if (world == null || !Store.IsAlive(world.Parent)
            || !ReferenceEquals(Store.GetComponent<BankAccountComponent>(world.Parent), world)
            || !world.Vault.Accounts.Contains(world)) return "utility world account is not attached.";
        return null;
    }
}
