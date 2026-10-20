using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.ECS.Queries;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Calender.Events;
using CitizenSimulator.Property.Components;
using CitizenSimulator.Property.Services.Abstractions;

namespace CitizenSimulator.Property.Services;

public class PropertySystem(IEventBus events, IPropertyService propertyService) : GameSystem
{
    private IQuery _propertyComponent = null!;
    private IDisposable? _onDayStart;
    protected override void OnStart(ICommandBuffer commands)
    {
        _propertyComponent = PropertyComponent.GetComponentType();
        _onDayStart = events.Subscribe<OnDayStart>(OnDayStart);
    }
    
    private void OnDayStart(OnDayStart eventData, SimulationTime time, ICommandBuffer commands)
    {
        foreach (var entity in Store.Query(_propertyComponent))
        {
            PropertyComponent property = Store.GetComponent<PropertyComponent>(entity);
            property.Condition -= (property.Owner == null ? 0.08 : 0.005);

            if (property.Condition <= 0)
                propertyService.DestroyProperty(property);
        }
    }
}