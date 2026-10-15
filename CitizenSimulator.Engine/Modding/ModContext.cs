using CitizenSimulator.Abstractions.Services;
using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Abstractions.Modding;

namespace CitizenSimulator.Engine.Modding;

public sealed class ModContext : IModContext
{
    public IServiceRegistry Services { get; }

    public ISimulationClock Clock { get; }

    public IEntityStore Store { get; }

    public IComponentRegistry Components { get; }

    public ISystemRegistry Systems { get; }
    
    public IEventBus Events { get; }

    public ModContext(IEntityStore store, IComponentRegistry components, ISystemRegistry systems, ISimulationClock clock, IEventBus events, IServiceRegistry services)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
        Clock = clock;
        Store = store;
        Components = components;
        Systems = systems;
        Events = events;
    }
}