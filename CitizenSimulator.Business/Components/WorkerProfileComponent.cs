using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Business.Components;

// Role skill levels are supplied by citizen setup/education; absent roles have skill zero.
public sealed class WorkerProfileComponent : Component<WorkerProfileComponent>
{
    public Dictionary<string, int> Skills { get; init; } = new(StringComparer.Ordinal);
}
