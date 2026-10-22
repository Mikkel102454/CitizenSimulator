using CitizenSimulator.CitizenMind.Evaluations;

namespace CitizenSimulator.CitizenMind.Planning;

public sealed class PlanResult
{
    public required PlanStatus Status { get; init; }
    public ActionPlan? Plan { get; init; }

    public EvaluationContext? PredictedState { get; init; }
    public IReadOnlyList<PlanReservation> Reservations { get; init; } = [];
}
