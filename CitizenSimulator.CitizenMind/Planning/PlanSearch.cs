using CitizenSimulator.CitizenMind.Definitions;
using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Planning.Abstractions;

namespace CitizenSimulator.CitizenMind.Planning;

/// <summary>A resumable uniform-cost search over one fixed observation snapshot.</summary>
public sealed class PlanSearch : IPlanningSearch
{
    private readonly GoalDefinition _goal;
    private readonly EvaluationContext _state;
    private readonly IReadOnlyDictionary<string, ActionDefinition> _actions;
    private readonly IReadOnlyDictionary<string, FactDescriptor> _descriptors;
    private readonly IReadOnlyDictionary<string, CandidateProviderSnapshot> _providers;
    private readonly PlannerOptions _options;
    private readonly IReadOnlyDictionary<string, IElapsedTimePredictor>? _timePredictors;
    private readonly PlanConstraints? _constraints;
    private readonly TimeSpan _latestAllowedTime;
    private readonly PriorityQueue<SearchNode, (double Cost, long Order)> _frontier = new();
    private readonly List<SearchNode> _known = [];
    private IReadOnlyList<ActionDefinition>? _allowedActions;
    private SearchNode? _current;
    private IPlanningSearch? _continuation;
    private bool _expandPending;
    private bool _initialized;
    private bool _depthLimitReached;
    private bool _timeLimitReached;
    private bool _continuationBudgetExceeded;
    private long _order;

    public int Expansions { get; private set; }
    public PlanResult? Result { get; private set; }

    internal PlanSearch(GoalDefinition goal, EvaluationContext state,
        IReadOnlyDictionary<string, ActionDefinition> actions,
        IReadOnlyDictionary<string, FactDescriptor> descriptors,
        IReadOnlyDictionary<string, CandidateProviderSnapshot> providers, PlannerOptions options,
        IReadOnlyDictionary<string, IElapsedTimePredictor>? timePredictors, PlanConstraints? constraints)
    {
        if (options.MaxDepth < 1) throw new ArgumentOutOfRangeException(nameof(options.MaxDepth));
        if (options.MaxExpansions < 1) throw new ArgumentOutOfRangeException(nameof(options.MaxExpansions));
        if (options.MaxCandidatesPerAction < 1) throw new ArgumentOutOfRangeException(nameof(options.MaxCandidatesPerAction));
        if (options.MaxPlanDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options.MaxPlanDuration));
        if (options.MaxScheduleChecks < 1) throw new ArgumentOutOfRangeException(nameof(options.MaxScheduleChecks));

        _goal = goal;
        _state = state;
        _actions = actions;
        _descriptors = descriptors;
        _providers = providers;
        _options = options;
        _timePredictors = timePredictors;
        _constraints = constraints;
        _latestAllowedTime = state.GameTime + options.MaxPlanDuration;
    }

    public void Advance(PlanningBudget budget)
    {
        while (Result == null)
        {
            if (_continuation != null)
            {
                _continuation.Advance(budget);
                if (_continuation.Result is not { } continuationResult)
                    return;

                _continuation = null;
                if (continuationResult.Status is PlanStatus.FOUND or PlanStatus.ALREADY_SATISFIED)
                {
                    FinishFound(continuationResult.Reservations);
                    return;
                }

                _continuationBudgetExceeded |= continuationResult.Status == PlanStatus.BUDGET_EXCEEDED;
                _expandPending = true;
            }

            if (!budget.TryConsume())
                return;

            if (!_initialized)
            {
                _initialized = true;
                if (_constraints?.GoalWindow is { } expired && _state.GameTime > expired.End)
                {
                    Result = new PlanResult { Status = PlanStatus.NO_PATH };
                    return;
                }

                _allowedActions = ActionDependencyResolver.Resolve(_goal, _actions);
                var start = new SearchNode { State = _state, TotalCost = 0 };
                _known.Add(start);
                _frontier.Enqueue(start, (0, _order++));
                continue;
            }

            if (_expandPending)
            {
                _expandPending = false;
                ExpandCurrent();
                _current = null;
                continue;
            }

            if (!_frontier.TryDequeue(out _current, out _))
            {
                Result = new PlanResult
                {
                    Status = _depthLimitReached || _timeLimitReached || _continuationBudgetExceeded
                        ? PlanStatus.BUDGET_EXCEEDED : PlanStatus.NO_PATH
                };
                return;
            }

            if (!_known.Contains(_current))
                continue;

            if (ActionPlanner.IsGoalSatisfied(_goal, _current.State) &&
                (_constraints?.GoalWindow is not { } window ||
                 (_current.State.GameTime >= window.Start && _current.State.GameTime <= window.End)))
            {
                var status = _constraints?.CheckContinuation?.Invoke(_current.State) ?? PlanStatus.FOUND;
                _continuationBudgetExceeded |= status == PlanStatus.BUDGET_EXCEEDED;
                if (status is PlanStatus.FOUND or PlanStatus.ALREADY_SATISFIED)
                {
                    if (_constraints?.CreateContinuation != null)
                    {
                        _continuation = _constraints.CreateContinuation(_current.State)
                            ?? throw new InvalidOperationException("A continuation search is required.");
                        continue;
                    }

                    FinishFound([]);
                    return;
                }
            }

            ExpandCurrent();
            _current = null;
        }
    }

    private void FinishFound(IReadOnlyList<PlanReservation> reservations)
    {
        Result = new PlanResult
        {
            Status = _current!.Parent == null ? PlanStatus.ALREADY_SATISFIED : PlanStatus.FOUND,
            Plan = _current.Parent == null ? null : ActionPlanner.BuildPlan(_current),
            PredictedState = _current.State,
            Reservations = reservations
        };
    }

    private void ExpandCurrent()
    {
        var current = _current!;
        if (current.Depth >= _options.MaxDepth)
        {
            _depthLimitReached = true;
            return;
        }
        if (Expansions >= _options.MaxExpansions)
        {
            Result = new PlanResult { Status = PlanStatus.BUDGET_EXCEEDED };
            return;
        }

        Expansions++;
        var waitTargets = new SortedSet<TimeSpan>();
        if (_constraints?.GoalWindow is { } goalWindow && goalWindow.Start > current.State.GameTime)
        {
            if (goalWindow.Start <= _latestAllowedTime) waitTargets.Add(goalWindow.Start);
            else _timeLimitReached = true;
        }

        foreach (var action in _allowedActions!)
        {
            var candidates = ActionPlanner.GetCandidates(action, current.State, _providers);
            if (candidates.Count > _options.MaxCandidatesPerAction)
            {
                Result = new PlanResult { Status = PlanStatus.BUDGET_EXCEEDED };
                return;
            }

            var descriptor = action.CandidateProvider == null ? null : _providers[action.CandidateProvider].Descriptor;
            ActionPlanner.ValidateCandidates(action, candidates, descriptor);
            foreach (var source in candidates.OrderBy(candidate => candidate.Id))
            {
                var candidate = ActionPlanner.SnapshotCandidate(source);
                foreach (var opening in candidate.GetUpcomingOpeningTimes(current.State.GameTime))
                {
                    if (_constraints?.GoalWindow is { } window && opening > window.End) continue;
                    if (opening > _latestAllowedTime) { _timeLimitReached = true; continue; }
                    waitTargets.Add(opening);
                }
                ExploreCandidate(current, candidate);
            }
        }

        if (waitTargets.Count > _options.MaxCandidatesPerAction)
        {
            Result = new PlanResult { Status = PlanStatus.BUDGET_EXCEEDED };
            return;
        }
        foreach (var target in waitTargets)
            ExploreCandidate(current, WaitCandidateFactory.Create(target));
    }

    private void ExploreCandidate(SearchNode parent, ActionCandidate candidate)
    {
        if (!ActionPlanner.TryEvaluateAction(candidate, parent.State, out var duration, out var cost)) return;
        var elapsed = TimeSpan.FromMinutes(duration);
        if (elapsed > _latestAllowedTime - parent.State.GameTime) { _timeLimitReached = true; return; }
        if (_constraints?.GoalWindow is { } window && elapsed > window.End - parent.State.GameTime) return;

        double totalCost = parent.TotalCost + cost;
        if (!double.IsFinite(totalCost)) throw new InvalidOperationException("Total plan cost must be finite.");
        var predicted = ActionPredictor.Predict(candidate, parent.State, duration, _descriptors, _timePredictors);
        if (ActionPlanner.HaveSameState(parent.State, predicted)) return;

        var child = new SearchNode
        {
            State = predicted, Parent = parent, Candidate = candidate,
            DurationMinutes = duration, TotalCost = totalCost, Depth = parent.Depth + 1
        };
        if (ActionPlanner.TryRememberNode(child, _known))
            _frontier.Enqueue(child, (child.TotalCost, _order++));
    }
}
