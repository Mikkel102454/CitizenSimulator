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
using CitizenSimulator.Needs.Models;

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
        var elapsed = calenderService.GetGameTime(1);

        foreach (var entity in Store.Query(_needsComponent))
        {
            var citizen = Store.GetComponent<CitizenComponent>(entity);
            if (!citizen.Alive) continue;
            
            NeedsComponent needs = Store.GetComponent<NeedsComponent>(entity);
            needs.Energy = NeedsDynamics.Advance(needs.Energy, NeedsDynamics.EnergyChangePerDay, elapsed);
            needs.Hunger = NeedsDynamics.Advance(needs.Hunger, NeedsDynamics.HungerChangePerDay, elapsed);
            needs.Thirst = NeedsDynamics.Advance(needs.Thirst, NeedsDynamics.ThirstChangePerDay, elapsed);

            if (needs.Hunger >= 1 || needs.Thirst >= 1 || needs.Energy <= 0)
            {
                citizenService.KillCitizen(citizen);
            }
        }
        
    }
    
    private void OnCitizenCreated(OnCitizenCreated eventData, SimulationTime time, ICommandBuffer commands)
    {
        if (Store.IsAlive(eventData.Citizen.Parent) && eventData.Citizen.Alive &&
            !Store.HasComponent<NeedsComponent>(eventData.Citizen.Parent))
            commands.AddComponent(eventData.Citizen.Parent, new NeedsComponent());
    }

    protected override void OnStop(ICommandBuffer commands) => _citizenCreated?.Dispose();
}
