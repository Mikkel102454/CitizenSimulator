using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Planning.Abstractions;
using CitizenSimulator.CitizenMind.Scheduling;

namespace CitizenSimulator.CitizenMind.Planning;

internal sealed class ScheduleForecastSearch(IPlanningSearch inner, ScheduledCommitment commitment,
    GoalDefinition goal, ActionPlan? prefixPlan = null) : IPlanningSearch
{
    public PlanResult? Result { get; private set; }

    public void Advance(PlanningBudget budget)
    {
        if (Result != null) return;
        inner.Advance(budget);
        if (inner.Result is not { } result) return;
        if (result.Status is not (PlanStatus.FOUND or PlanStatus.ALREADY_SATISFIED))
        {
            Result = result;
            return;
        }

        var plan = prefixPlan ?? result.Plan ?? new ActionPlan { PlanSteps = [] };
        Result = new PlanResult
        {
            Status = PlanStatus.FOUND,
            PredictedState = result.PredictedState,
            Reservations = new[] { new PlanReservation(commitment, goal, plan) }
                .Concat(result.Reservations).ToArray()
        };
    }
}
