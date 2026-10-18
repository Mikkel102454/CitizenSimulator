using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Citizen.Components;

namespace CitizenSimulator.Property.Components;

public sealed class PropertyComponent : Component<PropertyComponent>
{
    public enum PropertyType
    {
        Residential,
        Commercial,
        Industrial
    }
    
    public PropertyType Type { get; internal set; }

    public int Capacity { get; internal set; }

    public double BaseValue { get; internal set; }
        
    public CitizenComponent? Owner { get; internal set; }
        
    public double Condition { get; internal set; } = 100;
}