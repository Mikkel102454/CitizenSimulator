using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Property.Components;

namespace CitizenSimulator.Property.Services.Abstractions;

public interface IPropertyService
{
    bool CreateProperty(PropertyComponent.PropertyType type, int capacity, double baseValue, CitizenComponent? owner);
    List<PropertyComponent> GetProperties();
    bool TransferOwnership(PropertyComponent property, CitizenComponent? newOwner);
}