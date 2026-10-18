using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Citizen.Components;

namespace CitizenSimulator.Citizen.Services.Abstraction;

public interface ICitizenService
{
    bool CreateCitizen(EntityRef entityRef, string name, int age, out CitizenComponent? outCitizen);
    bool KillCitizen(CitizenComponent citizen);
}