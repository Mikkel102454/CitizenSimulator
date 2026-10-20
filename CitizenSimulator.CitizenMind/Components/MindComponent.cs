using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.CitizenMind.Models;

namespace CitizenSimulator.CitizenMind.Components;

public sealed class MindComponent : Component<MindComponent>
{
    public GoalDefinition? CurrentGoal { get; internal set; }
}