using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Citizen.Components;

public class CitizenComponent : Component<CitizenComponent>
{
    public string Name { get; internal set; } = "";
    public int Age { get; internal set; } = 0;
    public bool Alive { get; internal set; } = false;
}