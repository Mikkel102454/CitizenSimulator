using CitizenSimulator.CitizenMind.Components;
using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Models;

namespace CitizenSimulator.CitizenMind.Services;

public class GoalSelector(CitizenMindService citizenMindService)
{
    public GoalDefinition? SelectGoals(MindComponent mindComponent, ulong currentTick, bool emergenciesOnly = false)
    {
        Dictionary<GoalDefinition, double> eligibleGoals = new Dictionary<GoalDefinition, double>();
        Dictionary<GoalDefinition, int> emergencyGoals = new Dictionary<GoalDefinition, int>();
        
        var context = citizenMindService.CreateEvaluationContext(mindComponent.Parent);
        
        foreach (var goal in citizenMindService.Goals.Values)
        {
            bool satisfied = goal.Desired.All(condition => ExpressionEvaluator.Evaluate(context, condition));

            bool eligible = !satisfied && ExpressionEvaluator.Evaluate(context, goal.Eligible);

            bool isEmergency = eligible && goal.Emergency != null && ExpressionEvaluator.Evaluate(context, goal.Emergency.When);

            bool wasEmergency = mindComponent.EmergencyActive.TryGetValue(goal.Id, out var previous) && previous;

            mindComponent.EmergencyActive[goal.Id] = isEmergency;
            
            if (isEmergency && !wasEmergency) 
                mindComponent.GoalRetryTicks.Remove(goal.Id);

            if (mindComponent.GoalRetryTicks.TryGetValue(goal.Id, out var retryTick))
            {
                if (currentTick < retryTick)
                    continue;

                mindComponent.GoalRetryTicks.Remove(goal.Id);
            }

            if (!eligible)
                continue;

            if (isEmergency)
            {
                emergencyGoals.Add(goal, goal.Emergency!.Priority);
                continue;
            }

            if (!emergenciesOnly)
            {
                eligibleGoals.Add(goal, UtilityEvaluator.Evaluate(context, goal.Utility));
            }
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