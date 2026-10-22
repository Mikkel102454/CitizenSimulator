using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Abstractions.Services;

public interface IHomeAccessService
{
    bool CanSleep(EntityRef citizen);
    bool CanDrink(EntityRef citizen);
    bool CanCook(EntityRef citizen);
    bool CanStore(EntityRef citizen);
}
