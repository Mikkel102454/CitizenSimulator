using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.ECS.Queries;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Calender.Events;
using CitizenSimulator.Calender.Services.Abstractions;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Citizen.Services.Abstraction;
using CitizenSimulator.Needs.Components;

namespace CitizenSimulator.Needs.Systems;

public class NeedsSystem(IEventBus events, ICalenderService calenderService, ICitizenService citizenService) : GameSystem
{
    private IQuery _needsComponent = null!;
    protected override void OnStart(ICommandBuffer commandBuffer)
    {
        _needsComponent = NeedsComponent.GetComponentType();
    }

    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {
        foreach (var entity in Store.Query(_needsComponent))
        {
            NeedsComponent needs = Store.GetComponent<NeedsComponent>(entity);
            needs.Energy -= 30 / calenderService.GetTicksPerDay();
            needs.Hunger += 10 / calenderService.GetTicksPerDay();
            needs.Thirst += 34 / calenderService.GetTicksPerDay();

            if (needs.Hunger <= 0 || needs.Thirst <= 0 || needs.Energy <= 0)
            {
                CitizenComponent citizen = Store.GetComponent<CitizenComponent>(entity);
                citizenService.KillCitizen(citizen);
            }
        }
    }
}