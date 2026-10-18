using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Citizen.Events;
using CitizenSimulator.Citizen.Services.Abstraction;

namespace CitizenSimulator.Citizen.Services;

public sealed class CitizenService(IEntityStore store, IEventBus events) : ICitizenService
{
    public bool CreateCitizen(EntityRef entityRef, string name, int age, out CitizenComponent? outCitizen)
    {
        if (store.HasComponent<CitizenComponent>(entityRef))
        {
            outCitizen = null;
            return false;
        }
        
        outCitizen = store.AddComponent(entityRef, new CitizenComponent
        {
            Name = name,
            Age = age,
            Alive = true
        });

        events.Publish(new CitizenCreated(outCitizen));
        
        
        return true;
    }

    public bool KillCitizen(CitizenComponent citizen)
    {
        citizen.Alive = false;
        
        events.Publish(new CitizenDied(citizen));

        return true;
    }
}
