using CitizenSimulator.CitizenMind.Definitions;
using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Planning.Abstractions;
using CitizenSimulator.CitizenMind.Scheduling;

namespace CitizenSimulator.CitizenMind.Planning;

internal static class PlanValidator
{
    // Validation is atomic against one fresh snapshot. Reserve its whole cost
    // before beginning; it never starts another search outside the tick budget.
    public static int WorkCost(PlanResult candidate)
        => checked(1 + (candidate.Plan?.PlanSteps.Count ?? 0) +
            candidate.Reservations.Sum(reservation => 1 + reservation.Plan.PlanSteps.Count));

    public static PlanResult? Validate(GoalDefinition goal, PlanResult candidate, EvaluationContext live,
        IReadOnlyDictionary<string, FactDescriptor> descriptors,
        IReadOnlyDictionary<string, CandidateProviderSnapshot> providers,
        IReadOnlyDictionary<string, IElapsedTimePredictor> timePredictors, PlannerOptions options,
        TimeWindow? goalWindow, IReadOnlyList<ScheduledCommitment> remaining)
    {
        var horizon = live.GameTime + options.MaxPlanDuration;
        bool satisfied = InWindow(goalWindow, live.GameTime) && ActionPlanner.IsGoalSatisfied(goal, live);
        ActionPlan primary;
        EvaluationContext projected;
        if (satisfied)
        {
            primary = new ActionPlan { PlanSteps = [] };
            projected = live;
        }
        else
        {
            if (candidate.Plan == null || !Replay(candidate.Plan, live, horizon, goalWindow,
                    descriptors, providers, timePredictors, options, out primary, out projected) ||
                !InWindow(goalWindow, projected.GameTime) || !ActionPlanner.IsGoalSatisfied(goal, projected))
                return null;
        }

        var primaryState = projected;
        var reservations = new List<PlanReservation>();
        foreach (var commitment in remaining)
        {
            var proof = candidate.Reservations.FirstOrDefault(item => item.Commitment.Id == commitment.Id);
            if (proof == null || proof.Goal.Id != commitment.GoalId ||
                proof.Commitment.Window.Start != commitment.Window.Start ||
                proof.Commitment.Window.End != commitment.Window.End)
                return null;

            if (InWindow(commitment.Window, projected.GameTime) && ActionPlanner.IsGoalSatisfied(proof.Goal, projected))
            {
                reservations.Add(new PlanReservation(commitment, proof.Goal, new ActionPlan { PlanSteps = [] }));
                continue;
            }

            if (!Replay(proof.Plan, projected, horizon, commitment.Window,
                    descriptors, providers, timePredictors, options, out var refreshed, out projected) ||
                !InWindow(commitment.Window, projected.GameTime) || !ActionPlanner.IsGoalSatisfied(proof.Goal, projected))
                return null;

            reservations.Add(new PlanReservation(commitment, proof.Goal, refreshed));
        }

        return new PlanResult
        {
            Status = satisfied ? PlanStatus.ALREADY_SATISFIED : PlanStatus.FOUND,
            Plan = satisfied ? null : primary,
            PredictedState = primaryState,
            Reservations = reservations.AsReadOnly()
        };
    }

    private static bool InWindow(TimeWindow? window, TimeSpan time)
        => window == null || (time >= window.Start && time <= window.End);

    private static bool Replay(ActionPlan plan, EvaluationContext state, TimeSpan horizon, TimeWindow? window,
        IReadOnlyDictionary<string, FactDescriptor> descriptors,
        IReadOnlyDictionary<string, CandidateProviderSnapshot> providers,
        IReadOnlyDictionary<string, IElapsedTimePredictor> timePredictors, PlannerOptions options,
        out ActionPlan refreshed, out EvaluationContext projected)
    {
        projected = state;
        refreshed = new ActionPlan { PlanSteps = [] };
        var steps = new List<PlanStep>();
        double cost = 0;

        foreach (var step in plan.PlanSteps)
        {
            var candidate = step.Candidate;
            var action = candidate.Definition;
            if (action.CandidateProvider != null)
            {
                var candidates = ActionPlanner.GetCandidates(action, projected, providers);
                if (candidates.Count > options.MaxCandidatesPerAction) return false;
                ActionPlanner.ValidateCandidates(action, candidates, providers[action.CandidateProvider].Descriptor);
                candidate = candidates.FirstOrDefault(item => item.Id == candidate.Id);
                if (candidate == null) return false;
            }

            candidate = ActionPlanner.SnapshotCandidate(candidate);
            if (!ActionPlanner.TryEvaluateAction(candidate, projected, out var duration, out var actionCost)) return false;
            var elapsed = TimeSpan.FromMinutes(duration);
            if (elapsed > horizon - projected.GameTime ||
                (window != null && (projected.GameTime > window.End || elapsed > window.End - projected.GameTime)))
                return false;

            projected = ActionPredictor.Predict(candidate, projected, duration, descriptors, timePredictors);
            cost += actionCost;
            if (!double.IsFinite(cost)) throw new InvalidOperationException("Total plan cost must be finite.");
            steps.Add(new PlanStep { Candidate = candidate, DurationMinutes = duration });
        }

        refreshed = new ActionPlan { PlanSteps = steps.AsReadOnly(), TotalCost = cost };
        return true;
    }
}
