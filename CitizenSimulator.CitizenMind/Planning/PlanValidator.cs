using CitizenSimulator.CitizenMind.Definitions;
using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Planning.Abstractions;
using CitizenSimulator.CitizenMind.Scheduling;

namespace CitizenSimulator.CitizenMind.Planning;

internal static class PlanValidator
{
    public static int WorkCost(PlanResult candidate)
    {
        return checked(1 + (candidate.Plan?.PlanSteps.Count ?? 0) +
                       candidate.Reservations.Sum(reservation => 1 + reservation.Plan.PlanSteps.Count));
    }

    public static PlanResult? Validate(GoalDefinition goal, PlanResult candidate, EvaluationContext live,
        IReadOnlyDictionary<string, FactDescriptor> descriptors,
        IReadOnlyDictionary<string, CandidateProviderSnapshot> providers,
        IReadOnlyDictionary<string, IElapsedTimePredictor> timePredictors, PlannerOptions options,
        TimeWindow? goalWindow, IReadOnlyList<ScheduledCommitment> remaining)
    {
        TimeSpan horizon = live.GameTime + options.MaxPlanDuration;
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

        EvaluationContext primaryState = projected;
        var reservations = new List<PlanReservation>();
        foreach (ScheduledCommitment commitment in remaining)
        {
            PlanReservation? proof = candidate.Reservations.FirstOrDefault(item => item.Commitment.Id == commitment.Id);
            if (proof == null || proof.Goal.Id != commitment.GoalId ||
                proof.Commitment.Window.Start != commitment.Window.Start ||
                proof.Commitment.Window.End != commitment.Window.End || proof.Commitment.Target != commitment.Target)
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
    {
        return window == null || (time >= window.Start && time <= window.End);
    }

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

        foreach (PlanStep step in plan.PlanSteps)
        {
            ActionCandidate? candidate = step.Candidate;
            ActionDefinition action = candidate.Definition;

            if (action.CandidateProvider != null)
            {
                IReadOnlyList<ActionCandidate> candidates = ActionPlanner.GetCandidates(action, projected, providers);

                if (candidates.Count > options.MaxCandidatesPerAction)
                    return false;

                ActionPlanner.ValidateCandidates(action, candidates, providers[action.CandidateProvider].Descriptor);
                candidate = candidates.FirstOrDefault(item => item.Id == candidate.Id);

                if (candidate == null)
                    return false;
            }

            candidate = ActionPlanner.SnapshotCandidate(candidate);

            if (!ActionPlanner.TryEvaluateAction(candidate, projected, out var duration, out var actionCost))
                return false;

            var elapsed = TimeSpan.FromMinutes(duration);
            if (elapsed > horizon - projected.GameTime ||
                (window != null && (projected.GameTime > window.End || elapsed > window.End - projected.GameTime)))
                return false;

            projected = ActionPredictor.Predict(candidate, projected, duration, descriptors, timePredictors);
            cost += actionCost;

            if (!double.IsFinite(cost))
                throw new InvalidOperationException("Total plan cost must be finite.");

            steps.Add(new PlanStep
            {
                Candidate = candidate,
                DurationMinutes = duration
            });
        }

        refreshed = new ActionPlan { PlanSteps = steps.AsReadOnly(), TotalCost = cost };
        return true;
    }
}
