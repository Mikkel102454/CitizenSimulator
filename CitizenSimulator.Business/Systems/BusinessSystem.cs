using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.Calender.Services.Abstractions;

namespace CitizenSimulator.Business.Systems;

public class BusinessSystem(ICalenderService calender, IBusinessService businessService) : GameSystem(priority: -5)
{
    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {
        // The host commits Clock.Tick after callbacks; use this callback's destination tick.
        businessService.BeginTick(time.Tick);
        businessService.ReconcileCreations();
        var gameTime = calender.GetGameTime(time.Tick);
        foreach (var entity in Store.Query(BusinessComponent.GetComponentType()).OrderBy(candidate => candidate.Id))
        {
            businessService.AdvanceBusiness(Store.GetComponent<BusinessComponent>(entity)!, gameTime);
        }
    }

    protected override void OnStop(ICommandBuffer commands)
    {
        businessService.ReconcileCreations();
    }
}
