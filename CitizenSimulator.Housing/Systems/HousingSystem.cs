using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Housing.Services.Abstractions;

namespace CitizenSimulator.Housing.Systems;

public sealed class HousingSystem(IHousingService housing) : GameSystem(priority: -8)
{
    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {
        housing.Advance(time.Tick);
    }
}
