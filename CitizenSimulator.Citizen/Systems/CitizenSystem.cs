using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Abstractions.Timing;

namespace CitizenSimulator.Citizen.Systems;

public class CitizenSystem(IEventBus events) : GameSystem
{
    protected override void OnUpdate(SimulationTime time, ICommandBuffer commandBuffer)
    {

    }
}