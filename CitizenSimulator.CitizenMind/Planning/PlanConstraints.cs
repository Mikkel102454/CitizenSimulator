using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Planning.Abstractions;

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
    /// This callback must be a cheap synchronous predicate. Use CreateContinuation
    /// for searches that must share the scheduler's work allowance.
    /// </summary>
    public Func<EvaluationContext, PlanStatus>? CheckContinuation { get; init; }

    /// <summary>Creates a resumable feasibility check sharing the parent work budget.</summary>
    public Func<EvaluationContext, IPlanningSearch>? CreateContinuation { get; init; }
}
