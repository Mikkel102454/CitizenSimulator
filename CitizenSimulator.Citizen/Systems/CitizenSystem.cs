using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.ECS.Queries;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Calender.Events;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Citizen.Events;

namespace CitizenSimulator.Citizen.Systems;

public class CitizenSystem(IEventBus events) : GameSystem
{
    private IQuery _citizenComponent = null!;
    private IDisposable? _onDayStart;
    protected override void OnStart(ICommandBuffer commandBuffer)
    {
        _citizenComponent = CitizenComponent.GetComponentType();
        _onDayStart = events.Subscribe<OnDayStart>(OnDayStart);
    }
    
    private void OnDayStart(OnDayStart eventData, SimulationTime time, ICommandBuffer commandBuffer)
    {
        foreach (var entity in Store.Query(_citizenComponent))
        {
            CitizenComponent citizen = Store.GetComponent<CitizenComponent>(entity);

            if (eventData.Date.Equals(citizen.Birthday))
            {
                citizen.Age++;
                events.Publish(new CitizenBirthday(citizen));
            }
        }
    }
}