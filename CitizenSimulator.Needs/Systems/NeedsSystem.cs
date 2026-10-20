using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.ECS.Queries;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Calender.Events;
using CitizenSimulator.Calender.Services.Abstractions;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Citizen.Events;
using CitizenSimulator.Citizen.Services.Abstraction;
using CitizenSimulator.CitizenMind.Components;
using CitizenSimulator.Needs.Components;

namespace CitizenSimulator.Needs.Systems;

public class NeedsSystem(IEventBus events, ICalenderService calenderService, ICitizenService citizenService) : GameSystem
{
    private IQuery _needsComponent = null!;
    private IDisposable? _citizenCreated;
    protected override void OnStart(ICommandBuffer commands)
    {
        _needsComponent = NeedsComponent.GetComponentType();
        _citizenCreated = events.Subscribe<OnCitizenCreated>(OnCitizenCreated);
    }

    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {
        foreach (var entity in Store.Query(_needsComponent))
        {
            NeedsComponent needs = Store.GetComponent<NeedsComponent>(entity);
            needs.Energy -= .3 / calenderService.GetTicksPerDay();
            needs.Hunger += .1 / calenderService.GetTicksPerDay();
            needs.Thirst += .34 / calenderService.GetTicksPerDay();

            if (needs.Hunger <= 1 || needs.Thirst <= 1 || needs.Energy <= 0)
            {
                CitizenComponent citizen = Store.GetComponent<CitizenComponent>(entity);
                citizenService.KillCitizen(citizen);
            }
        }
        
        foreach (var entity in Store.Query(NeedsComponent.GetComponentType()))
        {
            NeedsComponent needs = Store.GetComponent<NeedsComponent>(entity);
            MindComponent mind = Store.GetComponent<MindComponent>(entity);
            Console.WriteLine(needs.Thirst);
            if(mind.CurrentGoal != null)
                Console.WriteLine(mind.CurrentGoal.Id);
        }
    }
    
    private void OnCitizenCreated(OnCitizenCreated eventData, SimulationTime time, ICommandBuffer commands)
    {
        commands.AddComponent(eventData.Citizen.Parent, new NeedsComponent());
    }
}