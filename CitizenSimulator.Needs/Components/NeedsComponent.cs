using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Needs.Components;

public sealed class NeedsComponent : Component<NeedsComponent>
{
    public float Hunger { get; internal set; } = 0f;
    public float Energy { get; internal set; } = 0f;
    public float Thirst { get; internal set; } = 0f;
}