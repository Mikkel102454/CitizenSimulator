using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Human.Components;

public class HumanComponent : Component<HumanComponent>
{
    public string Name { get; internal set; } = "";
    public int Age { get; internal set; } = 0;
    public bool Alive { get; internal set; } = false;
}