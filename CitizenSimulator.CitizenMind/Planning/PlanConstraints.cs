using CitizenSimulator.CitizenMind.Evaluations;

namespace CitizenSimulator.CitizenMind.Planning;

public sealed class PlanConstraints
{
    /// <summary>
    /// The goal must be satisfied within this window, including its end instant.
    /// Preparation actions may start earlier. Domain action availability controls
    /// when attendance or other time-bound activities themselves may run.
    /// </summary>
    public TimeWindow? GoalWindow { get; init; }

    /// <summary>
    /// Checks whether the goal state's remaining commitments are feasible.
    /// Return FOUND/ALREADY_SATISFIED to accept, NO_PATH to reject, or
    /// BUDGET_EXCEEDED when feasibility could not be established within limits.
    /// </summary>
    public Func<EvaluationContext, PlanStatus>? CheckContinuation { get; init; }
}
