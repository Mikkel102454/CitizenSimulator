using CitizenSimulator.Abstractions.Services;
using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Abstractions.Timing;

namespace CitizenSimulator.Abstractions.Modding;

public interface IModContext
{
    IServiceRegistry Services { get; }
    // Delivered between batches even while paused; held events are delivered once per tick.
    IEventBus InputEvents { get; }
    // Queued controls are safe to request from input handlers and system updates.
    ISimulationControls Controls { get; }

    ISimulationClock Clock { get; }

    IEntityStore Store { get; }

    IComponentRegistry Components { get; }

    ISystemRegistry Systems { get; }
    
    // required for mods to send events
    public IEventBus Events { get; }
}