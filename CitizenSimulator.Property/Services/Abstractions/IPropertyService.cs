using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Property.Components;

namespace CitizenSimulator.Property.Services.Abstractions;

public interface IPropertyService
{
    bool CreateProperty(ICommandBuffer commands,
        EntityRef entityRef,
        PropertyComponent.PropertyType type,
        int capacity,
        decimal areaM2,
        double baseValue,
        CitizenComponent? owner,
        out PropertyComponent? outProperty);
    bool TransferOwnership(PropertyComponent property, CitizenComponent? newOwner);
    bool DestroyProperty(PropertyComponent property);
}
