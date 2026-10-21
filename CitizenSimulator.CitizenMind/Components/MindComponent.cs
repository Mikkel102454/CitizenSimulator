using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.CitizenMind.Executor;
using CitizenSimulator.CitizenMind.Models;

namespace CitizenSimulator.CitizenMind.Components;

public sealed class MindComponent : Component<MindComponent>
{
    public GoalDefinition? CurrentGoal { get; internal set; }
    
    public PlanExecutionState? Execution { get; internal set; }
    
    public Dictionary<string, ulong> GoalRetryTicks { get; } = [];
    
    public bool IsActive { get; set; } = true;
}