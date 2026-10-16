using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Human.Components;
using CitizenSimulator.Human.Events;
using CitizenSimulator.Human.Services.Abstraction;

namespace CitizenSimulator.Human.Services;

public sealed class HumanService(IEntityStore store, IEventBus events) : IHumanService
{
    public EntityRef CreateHuman(EntityRef entityRef, string name, int age)
    {
        if(store.HasComponent<HumanComponent>(entityRef))
            return entityRef;
        
        store.AddComponent(entityRef, new HumanComponent
        {
            Name = name,
            Age = age,
            Alive = true
        });

        events.Publish(new HumanCreated(entityRef));
        
        
        return entityRef;
    }

    public EntityRef KillHuman(EntityRef entityRef)
    {
        var component =  store.GetComponent<HumanComponent>(entityRef);
        if(component == null)
            return entityRef;
        
        component.Alive = false;
        
        events.Publish(new HumanDied(entityRef));

        return entityRef;
    }
}
