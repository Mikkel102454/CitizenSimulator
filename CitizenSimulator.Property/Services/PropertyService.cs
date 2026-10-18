using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Property.Components;
using CitizenSimulator.Property.Events;
using CitizenSimulator.Property.Services.Abstractions;

namespace CitizenSimulator.Property.Services;

public class PropertyService(IEntityStore store, IEventBus events) : IPropertyService
{
    public bool CreateProperty(ICommandBuffer commands, EntityRef entityRef, PropertyComponent.PropertyType type, int capacity, 
        double baseValue, CitizenComponent? owner, out PropertyComponent? outProperty)
    {
        if (store.HasComponent<PropertyComponent>(entityRef))
        {
            outProperty = null;
            return false;
        }
        
        outProperty = commands.AddComponent(entityRef, new PropertyComponent());
        outProperty.Type = type;
        outProperty.Capacity = capacity;
        outProperty.BaseValue = baseValue;
        outProperty.Owner = owner;
        events.Publish(new OnPropertyCreated(outProperty));
        return true;
    }

    public bool TransferOwnership(PropertyComponent property, CitizenComponent? newOwner)
    {
        property.Owner = newOwner;
        events.Publish(new OnPropertyOwnershipChanged(property));
        return true;
    }

    public bool DestroyProperty(PropertyComponent property)
    {
        property.Destroyed = true;
        events.Publish(new OnPropertyDestroyed(property));
        return true;
    }
}