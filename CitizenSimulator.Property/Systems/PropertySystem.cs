using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.ECS.Queries;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Property.Components;

namespace CitizenSimulator.Property.Services;

public class PropertySystem(IEventBus events) : GameSystem
{
    private IQuery _propertyComponent = null!;
    protected override void OnStart(ICommandBuffer commandBuffer)
    {
        _propertyComponent = PropertyComponent.GetComponentType();
    }
    
    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {
        // this should change to per day or acttualy just listen to event
        foreach (var entity in Store.Query(_propertyComponent))
        {
            PropertyComponent component = Store.GetComponent<PropertyComponent>(entity);
            component.Condition -= (component.Owner == null ? 0.08 : 0.005);
        }
    }
}