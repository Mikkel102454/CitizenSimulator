using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Calender.Components;

namespace CitizenSimulator.Citizen.Components;

public sealed class CitizenComponent : Component<CitizenComponent>
{
    public string Name { get; internal set; } = "";
    public int Age { get; internal set; } = 0;
    public bool Alive { get; internal set; } = false;
    
    public CalenderComponent.Date Birthday { get; internal set; }
}