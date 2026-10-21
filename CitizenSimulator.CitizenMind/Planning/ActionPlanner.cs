using System.Runtime.Serialization;
using CitizenSimulator.CitizenMind.Definitions;
using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Models;

namespace CitizenSimulator.CitizenMind.Planning;

public class ActionPlanner
{
    public static bool TryEvaluateAction(ActionDefinition action, EvaluationContext state,
        out double durationMinutes, out double cost)
    {
        durationMinutes = 0;
        cost = 0;
        
        var context = new EvaluationContext
        {
            Facts = state.Facts,
            Parameters = action.Parameters,
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
        
        DurationConverter.GetMinutesPerUnit(action.Duration.Unit);
        
        if (!double.IsFinite(durationMinutes))
            throw new InvalidOperationException("Expected a finite number.");
        
        if (durationMinutes < 0)
            throw new InvalidOperationException("Expected a non-negative number.");
        
        context.Calculated.Add("durationMinutes", durationMinutes);
        
        object? costObject = ExpressionEvaluator.Resolve(context, action.Cost.Expression);
        
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
            PlannerOptions options)
    {
        if (options.MaxDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(options.MaxDepth));

        if (options.MaxExpansions < 1)
            throw new ArgumentOutOfRangeException(nameof(options.MaxExpansions));
        
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

        // we use this to always check the lowest cost path first
        var frontier = new PriorityQueue<SearchNode, (double Cost, long Order)>();
        
        long order = 0;
        frontier.Enqueue(start, (start.TotalCost, order++));
        
        int expansions = 0;
        bool depthLimitReached = false;
        
        while (frontier.TryDequeue(out var current, out _))
        {
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

            foreach (var action in allowedActions)
            {
                if (!TryEvaluateAction(action, current.State, out var duration, out var actionCost))
                {
                    continue;
                }

                double totalCost = current.TotalCost + actionCost;

                if (!double.IsFinite(totalCost))
                    throw new InvalidOperationException("Total plan cost must be finite.");

                var predicted = ActionPredictor.Predict(action, current.State, duration, descriptors);

                var child = new SearchNode
                {
                    State = predicted,
                    Parent = current,
                    Action = action,
                    DurationMinutes = duration,
                    TotalCost = totalCost,
                    Depth = current.Depth + 1
                };

                frontier.Enqueue(child, (child.TotalCost, order++));
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
                Action = current.Action ?? throw new InvalidOperationException("A child search node must have an action."),
                DurationMinutes = current.DurationMinutes
            });

            current = current.Parent;
        }

        steps.Reverse();

        return new ActionPlan
        {
            PlanSteps = steps,
            TotalCost = goalNode.TotalCost
        };
    }
}