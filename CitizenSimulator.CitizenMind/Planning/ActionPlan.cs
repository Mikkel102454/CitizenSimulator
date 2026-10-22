namespace CitizenSimulator.CitizenMind.Planning;

public sealed class ActionPlan
{
    public required IReadOnlyList<PlanStep> PlanSteps { get; init; }
    public double TotalCost { get; init; } 
}