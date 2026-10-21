using CitizenSimulator.CitizenMind.Planning;

namespace CitizenSimulator.CitizenMind.Executor;

public sealed class PlanExecutionState
{
    public required ActionPlan Plan { get; init; }
    
    public int StepIndex { get; set; } = 0;
    
    public bool Started { get; set; } = false;
    
    public TimeSpan Elapsed { get; set; } = TimeSpan.Zero;
    
    public Dictionary<string, object?> Before { get; set; } = [];
    
    public PlanExecutionStatus Status { get; set; } = PlanExecutionStatus.RUNNING;
}   