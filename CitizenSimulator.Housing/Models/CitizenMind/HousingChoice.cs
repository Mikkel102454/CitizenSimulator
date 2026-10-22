using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Housing.Models.CitizenMind;

public sealed record HousingChoice(EntityRef Home, Guid ListingId, decimal Price, bool Purchase,
    bool CanSleep, bool CanDrink, bool CanCook, bool CanStore);
