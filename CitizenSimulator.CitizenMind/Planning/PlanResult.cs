namespace CitizenSimulator.CitizenMind.Planning;

public sealed class PlanResult
{
    public required PlanStatus Status { get; init; }
    public ActionPlan? Plan { get; init; }
}