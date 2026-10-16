using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Abstractions.Timing;

namespace CitizenSimulator.Human.Systems;

public class HumanSystem(IEventBus events) : GameSystem
{
    protected override void OnUpdate(SimulationTime time, ICommandBuffer commandBuffer)
    {

    }
}