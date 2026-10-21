using CitizenSimulator.CitizenMind.Models;

namespace CitizenSimulator.CitizenMind.Planning;

public class ActionDependencyResolver
{
    
    private static void CollectFacts(ExpressionDefinition expression, HashSet<string> facts)
    {
        
        if (expression.Before != null)
            facts.Add(expression.Before);
        
        if (expression.Fact != null)
            facts.Add(expression.Fact);

        foreach (var argument in expression.Args)
            CollectFacts(argument, facts);
    }
    
    private static HashSet<string> CollectReadFacts(ActionDefinition action)
    {
        var facts = new HashSet<string>();

        foreach (var precondition in action.Preconditions)
            CollectFacts(precondition, facts);

        CollectFacts(action.Duration.Estimate, facts);
        CollectFacts(action.Cost.Expression, facts);

        foreach (var effect in action.Prediction)
        {
            CollectFacts(effect.Value, facts);

            // Arithmetic effects also read the fact being changed.
            if (PredictionOperations.ReadsTarget(effect.Op))
                facts.Add(effect.Fact);
        }

        foreach (var completion in action.Completion)
            CollectFacts(completion, facts);

        return facts;
    }

    private static Dictionary<string, List<ActionDefinition>> BuildWriters(IEnumerable<ActionDefinition> actions)
    {
        var writers = new Dictionary<string, List<ActionDefinition>>();

        foreach (var action in actions.OrderBy(
                     action => action.Id))
        {
            foreach (var fact in action.Prediction
                         .Select(effect => effect.Fact)
                         .Distinct())
            {
                if (!writers.TryGetValue(fact, out var list))
                {
                    list = [];
                    writers.Add(fact, list);
                }

                list.Add(action);
            }
        }

        return writers;
    }
    
    public static IReadOnlyList<ActionDefinition> Resolve(GoalDefinition goal, IReadOnlyDictionary<string, ActionDefinition> actions)
    {
        var writers = BuildWriters(actions.Values);

        var visited = new HashSet<string>();
        var pending = new Queue<ActionDefinition>();
        var result = new List<ActionDefinition>();

        foreach (var id in goal.GoalActions
                     .Distinct()
                     .OrderBy(id => id))
        {
            if (!actions.TryGetValue(id, out var action))
                throw new InvalidOperationException($"Goal '{goal.Id}' references unknown action '{id}'.");

            if (visited.Add(id))
                pending.Enqueue(action);
        }

        while (pending.TryDequeue(out var action))
        {
            result.Add(action);

            foreach (var fact in CollectReadFacts(action)
                         .OrderBy(fact => fact))
            {
                if (!writers.TryGetValue(fact, out var candidates))
                    continue;

                foreach (var candidate in candidates)
                {
                    if (visited.Add(candidate.Id)) 
                        pending.Enqueue(candidate);
                }
            }
        }

        return result.OrderBy(action => action.Id).ToList();
    }
}