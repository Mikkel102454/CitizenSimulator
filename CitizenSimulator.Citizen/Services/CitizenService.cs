using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Citizen.Events;
using CitizenSimulator.Citizen.Services.Abstraction;
using CitizenSimulator.CitizenMind.Components;

namespace CitizenSimulator.Citizen.Services;

public sealed class CitizenService(IEntityStore store, IEventBus events) : ICitizenService
{
    private uint nextId = 1;
    public bool CreateCitizen(ICommandBuffer commands, EntityRef entityRef, string name, int age, out CitizenComponent? outCitizen)
    {
        if (store.HasComponent<CitizenComponent>(entityRef))
        {
            outCitizen = null;
            return false;
        }

        outCitizen = commands.AddComponent(entityRef, new CitizenComponent
        {
            Id = nextId++,
            Name = name,
            Age = age,
            Alive = true
        });
        commands.AddComponent(entityRef, new MindComponent());

        events.Publish(new OnCitizenCreated(outCitizen));


        return true;
    }

    public bool KillCitizen(CitizenComponent citizen)
    {
        if (!citizen.Alive)
            return false;

        citizen.Alive = false;

        var mind = store.GetComponent<MindComponent>(citizen.Parent);

        if (mind != null)
            mind.IsActive = false;

        events.Publish(new OnCitizenDied(citizen));

        return true;
    }
}
