using CitizenSimulator.CitizenMind.Components;
using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Models;

namespace CitizenSimulator.CitizenMind.Services;

public class GoalSelector(CitizenMindService citizenMindService)
{
    public GoalDefinition? SelectGoals(MindComponent mindComponent)
    {
        Dictionary<GoalDefinition, double> eligibleGoals = new Dictionary<GoalDefinition, double>();
        Dictionary<GoalDefinition, int> emergencyGoals = new Dictionary<GoalDefinition, int>();
        
        var context = citizenMindService.CreateEvaluationContext(mindComponent.Parent);
        
        foreach (var goal in citizenMindService.Goals.Values)
        {
            if (! ExpressionEvaluator.Evaluate(context, goal.Eligible))
            {
                continue;
            }
            if (goal.Emergency != null && ExpressionEvaluator.Evaluate(context, goal.Emergency.When))
            {
                emergencyGoals.Add(goal, goal.Emergency.Priority);
                continue;
            }
            eligibleGoals.Add(goal, UtilityEvaluator.Evaluate(context, goal.Utility));
        }

        //always run emergency first
        if (emergencyGoals.Count > 0)
        {
            // for now alwyas take gihest prio
            foreach (var goal in emergencyGoals.OrderByDescending(x => x.Value))
            {
                return goal.Key;
            }
        }
        
        foreach (var goal in eligibleGoals.OrderByDescending(x => x.Value))
        {
            return goal.Key;
        }
        return null;
    }
}