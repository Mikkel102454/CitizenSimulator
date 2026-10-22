using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Utilities.Services.Abstractions;

namespace CitizenSimulator.Utilities.Systems;

public sealed class HomeUtilitySystem(IUtilityService utilities) : GameSystem(priority: 11)
{
    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {
        utilities.SupplyHomes();
    }
}
