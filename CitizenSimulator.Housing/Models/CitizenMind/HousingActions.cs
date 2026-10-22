using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Abstractions.Services;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.CitizenMind.Configuration;
using CitizenSimulator.CitizenMind.Services.Abstractions;
using CitizenSimulator.Housing.Components;
using CitizenSimulator.Housing.Services.Abstractions;

namespace CitizenSimulator.Housing.Models.CitizenMind;

public static class HousingActions
{
    public const string HasHomeFact = "housing.hasHome";
    public const string CanFindHomeFact = "housing.canFindHome";
    public const string DailyRentFact = "housing.dailyRent";
    public const string FindHomeGoal = "housing.find_home";
    public const string MoveAction = "housing.move_home";
    public const string BuyAction = "housing.buy_home";
    public const string Provider = "housing.homes";

    public static void Register(IModContext context)
    {
        var minds = context.Services.Get<ICitizenMindService>();
        var housing = context.Services.Get<IHousingService>();
        CitizenComponent? GetCitizen(EntityRef entity) => context.Store.IsAlive(entity)
            ? context.Store.GetComponent<CitizenComponent>(entity) : null;
        minds.RegisterFact(HasHomeFact, entity => GetCitizen(entity) is { } citizen
            && housing.GetResidence(citizen) is { House.Property.Destroyed: false } ? 1.0 : 0.0, canPredict: true);
        minds.RegisterFact(DailyRentFact, entity => GetCitizen(entity) is { } citizen
            ? housing.GetResidence(citizen)?.Lease?.DailyRent ?? 0m : 0m, canPredict: true);
        minds.RegisterFact(CanFindHomeFact, entity => GetCitizen(entity) is { Alive: true } citizen
            && housing.GetResidence(citizen) == null
            && (housing.FindHomes(citizen).Count > 0 || housing.FindHomePurchases(citizen).Count > 0));
        minds.RegisterTimePredictor("housing.rent", new HousingRentPredictor());
        minds.RegisterCandidateProvider(new()
        {
            Id = Provider,
            BindingTypes = new Dictionary<string, Type>
            {
                ["home"] = typeof(EntityRef),
                ["listingId"] = typeof(Guid),
                ["price"] = typeof(decimal),
                ["canSleep"] = typeof(bool),
                ["canDrink"] = typeof(bool),
                ["canCook"] = typeof(bool),
                ["canStore"] = typeof(bool)
            }
        }, entity => new HousingCandidateProvider(GetChoices(context, housing, GetCitizen(entity))));
        minds.RegisterHandler(MoveAction, new MoveHomeHandler(context.Store, housing, purchase: false));
        minds.RegisterHandler(BuyAction, new MoveHomeHandler(context.Store, housing, purchase: true));
        MindDefinitionLoader.Load(minds, typeof(HousingActions).Assembly);
    }

    private static IReadOnlyList<HousingChoice> GetChoices(IModContext context, IHousingService housing, CitizenComponent? citizen)
    {
        if (citizen is not { Alive: true } || housing.GetResidence(citizen) != null)
            return [];

        var choices = new List<HousingChoice>();
        var rentals = housing.GetRentalListings();
        foreach (HouseComponent house in housing.FindHomes(citizen))
        {
            if (ReferenceEquals(house.Property.Owner, citizen))
            {
                choices.Add(CreateChoice(context, house, Guid.Empty, 0m, purchase: false));
                continue;
            }

            var rental = rentals.FirstOrDefault(listing => ReferenceEquals(listing.House, house));
            if (rental != null)
                choices.Add(CreateChoice(context, house, rental.Id, rental.DailyRent, purchase: false));
        }

        foreach (var sale in housing.FindHomePurchases(citizen))
        {
            var house = context.Store.GetComponent<HouseComponent>(sale.Property.Parent)!;
            choices.Add(CreateChoice(context, house, sale.Id, sale.Price, purchase: true));
        }

        return choices;
    }

    private static HousingChoice CreateChoice(IModContext context, HouseComponent house, Guid listing, decimal price, bool purchase)
    {
        bool metered = context.Services.TryGet<IHomeUtilityService>(out var utilities);
        bool water = house.HasSafeWater && (!metered || utilities!.HasWater(house.Parent));
        bool electricity = !metered || utilities!.HasElectricity(house.Parent);
        return new HousingChoice(house.Parent, listing, price, purchase, house.HasBeds,
            water, water && electricity && house.HasKitchen, house.StorageCapacity > 0);
    }
}
