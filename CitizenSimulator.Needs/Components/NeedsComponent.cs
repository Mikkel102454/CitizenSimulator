using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Needs.Components;

public sealed class NeedsComponent : Component<NeedsComponent>
{
    public double Hunger { get; internal set; } = 0f;
    
    public double Thirst { get; internal set; } = 0;
    
    public double Energy { get; internal set; } = 1f;
}