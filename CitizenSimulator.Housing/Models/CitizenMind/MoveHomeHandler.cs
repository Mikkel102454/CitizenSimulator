using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Models.Abstractions;
using CitizenSimulator.Housing.Components;
using CitizenSimulator.Housing.Services.Abstractions;

namespace CitizenSimulator.Housing.Models.CitizenMind;

public sealed class MoveHomeHandler(IEntityStore store, IHousingService housing, bool purchase) : IMindActionHandler
{
    public ActionResult Start(ActionContext context) => CanMove(context) ? ActionResult.RUNNING : ActionResult.FAILED;

    public ActionResult Tick(ActionContext context)
    {
        if (!CanMove(context))
            return ActionResult.FAILED;

        double minutes = context.GetParameter<double>("durationMinutes");
        if (context.Elapsed.TotalMinutes < minutes)
            return ActionResult.RUNNING;

        var citizen = store.GetComponent<CitizenComponent>(context.Citizen)!;
        var house = store.GetComponent<HouseComponent>((EntityRef)context.Bindings["home"]!)!;
        Guid listingId = (Guid)context.Bindings["listingId"]!;
        decimal price = (decimal)context.Bindings["price"]!;
        bool moved = purchase ? housing.TryBuyHome(citizen, listingId, price)
            : housing.TryMoveIn(citizen, house, listingId == Guid.Empty ? null : listingId);
        return moved ? ActionResult.SUCCEEDED : ActionResult.FAILED;
    }

    public void Cancel(ActionContext context)
    {
        // No reservation, lease, payment or ownership change exists before completion.
    }

    private bool CanMove(ActionContext context)
    {
        if (!store.IsAlive(context.Citizen) || store.GetComponent<CitizenComponent>(context.Citizen) is not { Alive: true } citizen
            || !context.Bindings.TryGetValue("home", out var homeValue) || homeValue is not EntityRef home
            || !store.IsAlive(home) || store.GetComponent<HouseComponent>(home) is not { } house
            || !context.Bindings.TryGetValue("listingId", out var idValue) || idValue is not Guid listingId
            || !context.Bindings.TryGetValue("price", out var priceValue) || priceValue is not decimal price
            || housing.GetResidence(citizen) != null)
            return false;

        if (purchase)
            return housing.FindHomePurchases(citizen).Any(sale => sale.Id == listingId
                && sale.Price == price && ReferenceEquals(sale.Property, house.Property));

        if (!housing.FindHomes(citizen).Contains(house))
            return false;

        if (ReferenceEquals(house.Property.Owner, citizen))
            return listingId == Guid.Empty && price == 0;

        return housing.GetRentalListings().Any(listing => listing.Id == listingId
            && ReferenceEquals(listing.House, house) && listing.DailyRent == price);
    }
}
