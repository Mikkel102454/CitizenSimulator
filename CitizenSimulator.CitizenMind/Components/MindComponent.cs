using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.CitizenMind.Executor;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Scheduling;

namespace CitizenSimulator.CitizenMind.Components;

public sealed class MindComponent : Component<MindComponent>
{
    public GoalDefinition? CurrentGoal { get; internal set; }
    
    public PlanExecutionState? Execution { get; internal set; }
    
    public Dictionary<string, ulong> GoalRetryTicks { get; } = [];
    
    public bool IsActive { get; set; } = true;
    
    public Dictionary<string, bool> EmergencyActive { get; } = [];

    public ScheduledCommitmentState? CurrentCommitment { get; internal set; }

    internal List<ScheduledCommitmentState> Schedule { get; set; } = [];
    internal long ScheduleRevision { get; set; }
    internal long PlanScheduleRevision { get; set; }
}
