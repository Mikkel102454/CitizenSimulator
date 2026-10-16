using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Human.Services.Abstraction;

public interface IHumanService
{
    EntityRef CreateHuman(EntityRef entityRef, string name, int age);
    EntityRef KillHuman(EntityRef entityRef);
}