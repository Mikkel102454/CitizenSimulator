using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Abstractions.Timing;

namespace CitizenSimulator.Abstractions.Modding;

public interface IModContext
{
    ISimulationClock Clock { get; }

    IEntityStore Store { get; }

    IComponentRegistry Components { get; }

    ISystemRegistry Systems { get; }
    
    // required for mods to send events
    public IEventBus Events { get; }
}