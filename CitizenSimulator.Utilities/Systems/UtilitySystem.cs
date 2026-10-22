using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Utilities.Services.Abstractions;

namespace CitizenSimulator.Utilities.Systems;

public sealed class UtilitySystem(IUtilityService utilities) : GameSystem(priority: -7)
{
    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {
        utilities.Advance(time.Tick, commands);
    }
}
