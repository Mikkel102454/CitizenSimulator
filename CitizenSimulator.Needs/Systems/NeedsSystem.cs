using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Abstractions.Timing;

namespace CitizenSimulator.Needs.Systems;

public class NeedsSystem(IEventBus events) : GameSystem
{
    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {
        base.OnUpdate(time, commands);
    }
}