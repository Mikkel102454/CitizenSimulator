namespace CitizenSimulator.CitizenMind.Planning;

public sealed class ActionPlan
{
    public List<PlanStep> PlanSteps { get; init; } = [];
    public double TotalCost { get; init; } 
}