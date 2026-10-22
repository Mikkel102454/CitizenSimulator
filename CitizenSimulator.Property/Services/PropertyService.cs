using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Property.Components;
using CitizenSimulator.Property.Events;
using CitizenSimulator.Property.Services.Abstractions;

namespace CitizenSimulator.Property.Services;

public class PropertyService(IEntityStore store, IEventBus events) : IPropertyService
{
    public bool CreateProperty(ICommandBuffer commands,
        EntityRef entityRef,
        PropertyComponent.PropertyType type,
        int capacity,
        decimal areaM2,
        double baseValue,
        CitizenComponent? owner,
        out PropertyComponent? outProperty)
    {
        if (areaM2 <= 0 || capacity < 0 || !Enum.IsDefined(type)
            || !double.IsFinite(baseValue) || baseValue < 0
            || (owner != null && !IsLiving(owner)) || store.HasComponent<PropertyComponent>(entityRef))
        {
            outProperty = null;
            return false;
        }

        outProperty = commands.AddComponent(entityRef, new PropertyComponent());
        outProperty.Type = type;
        outProperty.Capacity = capacity;
        outProperty.AreaM2 = areaM2;
        outProperty.BaseValue = baseValue;
        outProperty.Owner = owner;
        events.Publish(new OnPropertyCreated(outProperty));
        return true;
    }

    public bool TransferOwnership(PropertyComponent property, CitizenComponent? newOwner)
    {
        if (!IsRegistered(property) || (newOwner != null && !IsLiving(newOwner)))
            return false;

        property.Owner = newOwner;
        events.Publish(new OnPropertyOwnershipChanged(property));
        return true;
    }

    public bool DestroyProperty(PropertyComponent property)
    {
        if (!IsRegistered(property) || property.Destroyed)
            return false;

        property.Destroyed = true;
        events.Publish(new OnPropertyDestroyed(property));
        return true;
    }

    public bool RepairProperty(PropertyComponent property, double conditionPoints)
    {
        if (!IsRegistered(property) || property.Destroyed || !double.IsFinite(property.Condition)
            || !double.IsFinite(conditionPoints) || conditionPoints <= 0
            || conditionPoints > 100 || property.Condition + conditionPoints > 100)
            return false;

        property.Condition += conditionPoints;
        return true;
    }

    private bool IsRegistered(PropertyComponent property) => store.IsAlive(property.Parent)
        && ReferenceEquals(store.GetComponent<PropertyComponent>(property.Parent), property);

    private bool IsLiving(CitizenComponent citizen) => citizen.Alive && store.IsAlive(citizen.Parent)
        && ReferenceEquals(store.GetComponent<CitizenComponent>(citizen.Parent), citizen);
}
