using System.Runtime.Serialization;
using CitizenSimulator.CitizenMind.Definitions;
using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Planning.Abstractions;

namespace CitizenSimulator.CitizenMind.Planning;

public class ActionPlanner
{
    public static bool TryEvaluateAction(ActionDefinition action, EvaluationContext state, 
        out double durationMinutes, out double cost)
    {
        return TryEvaluateAction(
            new ActionCandidate
            {
                Id = action.Id,
                Definition = action
            },
            state, out durationMinutes, out cost);
    }
    
    public static bool TryEvaluateAction(ActionCandidate candidate, EvaluationContext state,
        out double durationMinutes, out double cost)
    {
        durationMinutes = 0;
        cost = 0;
        
        var action = candidate.Definition;
        
        var context = new EvaluationContext
        {
            Facts = state.Facts,
            Parameters = action.ResolvedParameters,
            Bindings = candidate.Bindings,
            GameTime = state.GameTime,
            Calculated = []
        };

        foreach (var precondition in action.Preconditions)
        {
            if (!ExpressionEvaluator.Evaluate(context, precondition))
                return false;
        }

        object? durationObject = ExpressionEvaluator.Resolve(context, action.Duration.Estimate);
        
        if (durationObject is not (int or float or double or decimal))
            throw new InvalidOperationException("Duration must be numeric.");
        
        double duration = Convert.ToDouble(durationObject);

        durationMinutes = DurationConverter.ToMinutes(duration, action.Duration.Unit);
        
        if (!candidate.IsAvailable(state.GameTime, TimeSpan.FromMinutes(durationMinutes)))
            return false;
        
        context.Calculated.Add("durationMinutes", durationMinutes);
        
        object? costObject = ExpressionEvaluator.Resolve(context, action.Cost.Expression, ExpressionEvaluationMode.Score);
        
        if (costObject is not (int or float or double or decimal))
            throw new InvalidOperationException("Cost must be numeric.");

        double costDouble = Convert.ToDouble(costObject);
        
        if (!double.IsFinite(costDouble))
            throw new InvalidOperationException("Expected a finite number.");
        
        if (costDouble < 0)
            throw new InvalidOperationException("Expected a non-negative number.");

        cost = costDouble;
        
        return true;
    }

    public static PlanResult FindPlan(GoalDefinition goal, EvaluationContext state,
        IReadOnlyDictionary<string, ActionDefinition> actions, IReadOnlyDictionary<string, FactDescriptor> descriptors,
        IReadOnlyDictionary<string, CandidateProviderSnapshot> providers, PlannerOptions options)
    {
        if (options.MaxDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(options.MaxDepth));

        if (options.MaxExpansions < 1)
            throw new ArgumentOutOfRangeException(nameof(options.MaxExpansions));
        
        
        if (options.MaxCandidatesPerAction < 1)
            throw new ArgumentOutOfRangeException(nameof(options.MaxCandidatesPerAction));
        
        if(IsGoalSatisfied(goal, state))
            return new PlanResult()
            {
                Status = PlanStatus.ALREADY_SATISFIED,
            };
        
        var allowedActions = ActionDependencyResolver.Resolve(goal, actions);
        
        var start = new SearchNode
        {
            State = state,
            TotalCost = 0
        };
        
        var known = new List<SearchNode> { start };

        // we use this to always check the lowest cost path first
        var frontier = new PriorityQueue<SearchNode, (double Cost, long Order)>();
        
        long order = 0;
        
        frontier.Enqueue(start, (start.TotalCost, order++));
        
        int expansions = 0;
        bool depthLimitReached = false;
        
        void ExploreCandidate(SearchNode parent, ActionCandidate candidate)
        {
            if (!TryEvaluateAction(candidate, parent.State, out var duration, out var actionCost))
                return;


            double totalCost = parent.TotalCost + actionCost;

            if (!double.IsFinite(totalCost))
                throw new InvalidOperationException("Total plan cost must be finite.");

            var predicted = ActionPredictor.Predict(candidate, parent.State, duration, descriptors);

            if (HaveSameState(parent.State, predicted))
                return;

            var child = new SearchNode
            {
                State = predicted,
                Parent = parent,
                Candidate = candidate,
                DurationMinutes = duration,
                TotalCost = totalCost,
                Depth = parent.Depth + 1
            };

            if (!TryRememberNode(child, known))
                return;

            frontier.Enqueue(child, (child.TotalCost, order++));
        }
        
        while (frontier.TryDequeue(out var current, out _))
        {
            if (!known.Contains(current))
                continue;
            
            if (IsGoalSatisfied(goal, current.State))
            {
                return new PlanResult
                {
                    Status = PlanStatus.FOUND,
                    Plan = BuildPlan(current)
                };
            }
            
            if (current.Depth >= options.MaxDepth)
            {
                depthLimitReached = true;
                continue;
            }
            
            
            if (expansions >= options.MaxExpansions)
            {
                return new PlanResult
                {
                    Status = PlanStatus.BUDGET_EXCEEDED
                };
            }

            expansions++;

            var waitTargets = new SortedSet<TimeSpan>();
            
            foreach (var action in allowedActions)
            {
                var candidates = GetCandidates(action, current.State, providers);
                
                if (candidates.Count > options.MaxCandidatesPerAction)
                {
                    return new PlanResult
                    {
                        Status = PlanStatus.BUDGET_EXCEEDED
                    };
                }
                
                var providerDescriptor = action.CandidateProvider == null ? null : providers[action.CandidateProvider].Descriptor;

                ValidateCandidates(action, candidates, providerDescriptor);

                foreach (var sourceCandidate in candidates.OrderBy(candidate => candidate.Id))
                {
                    var candidate = SnapshotCandidate(sourceCandidate);
                    
                    foreach (var opening in candidate.GetUpcomingOpeningTimes(current.State.GameTime))
                        waitTargets.Add(opening);
                    
                    ExploreCandidate(current, candidate);
                }
            }
            
            if (waitTargets.Count > options.MaxCandidatesPerAction)
            {
                return new PlanResult
                {
                    Status = PlanStatus.BUDGET_EXCEEDED
                };
            }
            
            foreach (var target in waitTargets)
            {
                ExploreCandidate(
                    current,
                    WaitCandidateFactory.Create(target));
            }
        }
        
        return new PlanResult
        {
            Status = depthLimitReached ? PlanStatus.BUDGET_EXCEEDED : PlanStatus.NO_PATH
        };
    }

    private static bool IsGoalSatisfied(GoalDefinition goal, EvaluationContext state)
    {
        foreach (var desired in goal.Desired)
        {
            if(!ExpressionEvaluator.Evaluate(state, desired))
                return false;
        }
        return true;
    }
    
    private static ActionPlan BuildPlan(SearchNode goalNode)
    {
        var steps = new List<PlanStep>();
        var current = goalNode;

        while (current.Parent != null)
        {
            steps.Add(new PlanStep
            {
                Candidate = current.Candidate ?? throw new InvalidOperationException("A child search node must have a candidate."),
                DurationMinutes = current.DurationMinutes
            });

            current = current.Parent;
        }

        steps.Reverse();

        return new ActionPlan
        {
            PlanSteps = steps.AsReadOnly(),
            TotalCost = goalNode.TotalCost
        };
    }
    
    private static IReadOnlyList<ActionCandidate> GetCandidates(ActionDefinition action, EvaluationContext state, 
        IReadOnlyDictionary<string, CandidateProviderSnapshot> providers)
    {
        if (action.CandidateProvider == null)
        {
            return
            [
                new ActionCandidate
                {
                    Id = action.Id,
                    Definition = action
                }
            ];
        }

        if (!providers.TryGetValue(action.CandidateProvider, out var snapshot))
            throw new InvalidOperationException($"Unknown candidate provider '{action.CandidateProvider}'.");

        var candidates = snapshot.Provider.GetCandidates(action, state) 
                         ?? throw new InvalidOperationException($"Provider '{action.CandidateProvider}' returned null.");

        return candidates;
    }
    
    private static void ValidateCandidates(ActionDefinition action, IReadOnlyList<ActionCandidate> candidates, CandidateProviderDescriptor? descriptor)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            if (candidate == null)
                throw new InvalidOperationException($"Action '{action.Id}': provider returned a null candidate.");

            if (string.IsNullOrWhiteSpace(candidate.Id))
                throw new InvalidOperationException($"Action '{action.Id}': candidate ID cannot be blank.");

            if (!ids.Add(candidate.Id))
                throw new InvalidOperationException($"Action '{action.Id}': duplicate candidate '{candidate.Id}'.");

            if (!ReferenceEquals(candidate.Definition, action))
                throw new InvalidOperationException($"Candidate '{candidate.Id}' references the wrong definition.");

            if (candidate.Bindings == null)
                throw new InvalidOperationException($"Candidate '{candidate.Id}': bindings cannot be null.");

            
            if (descriptor == null)
            {
                if (candidate.Bindings.Count != 0)
                    throw new InvalidOperationException($"Candidate '{candidate.Id}' has bindings without a provider.");

                continue;
            }

            foreach (var (name, expectedType) in descriptor.BindingTypes)
            {
                if (!candidate.Bindings.TryGetValue(name, out var value))
                    throw new InvalidOperationException($"Candidate '{candidate.Id}' is missing binding '{name}'.");

                if (value == null || !expectedType.IsInstanceOfType(value))
                    throw new InvalidOperationException($"Candidate '{candidate.Id}' binding '{name}' " + $"must have type '{expectedType.Name}'.");

                if ((value is double number && !double.IsFinite(number)) || (value is float single && !float.IsFinite(single)))
                {
                    throw new InvalidOperationException(
                        $"Candidate '{candidate.Id}' binding '{name}' must be finite.");
                }
            }

            foreach (var name in candidate.Bindings.Keys)
            {
                if (!descriptor.BindingTypes.ContainsKey(name))
                    throw new InvalidOperationException($"Candidate '{candidate.Id}' has undeclared binding '{name}'.");
            }
            
            foreach (var key in candidate.Bindings.Keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                    throw new InvalidOperationException($"Candidate '{candidate.Id}': binding key cannot be blank.");
            }
        }
    }
    
    private static bool HaveSameState(EvaluationContext first, EvaluationContext second)
    {
        return first.GameTime == second.GameTime && HaveSameFacts(first.Facts, second.Facts);
    }
    
    private static bool HaveSameFacts(IReadOnlyDictionary<string, object?> first, IReadOnlyDictionary<string, object?> second)
    {
        
        if (first.Count != second.Count)
            return false;

        foreach (var (name, value) in first)
        {
            if (!second.TryGetValue(name, out var other) || !Equals(value, other))
                return false;
        }

        return true;
    }
    
    private static bool TryRememberNode(SearchNode candidate, List<SearchNode> known)
    {
        foreach (var existing in known)
        {
            if (existing.TotalCost <= candidate.TotalCost && existing.Depth <= candidate.Depth && 
                HaveSameState(existing.State, candidate.State))
            {
                return false;
            }
        }

        known.RemoveAll(existing =>
            candidate.TotalCost <= existing.TotalCost &&
            candidate.Depth <= existing.Depth &&
            HaveSameState(candidate.State, existing.State));

        known.Add(candidate);
        return true;
    }
    
    private static ActionCandidate SnapshotCandidate(ActionCandidate source)
    {
        var bindings = new Dictionary<string, object?>(source.Bindings);

        return new ActionCandidate
        {
            Id = source.Id,
            Definition = source.Definition,
            Bindings = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(bindings),
            Availability = source.Availability?.ToList().AsReadOnly(),
            DailyAvailability = source.DailyAvailability?.ToList().AsReadOnly(),
        };
    }
}