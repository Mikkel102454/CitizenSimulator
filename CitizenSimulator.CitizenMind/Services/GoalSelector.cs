using CitizenSimulator.CitizenMind.Components;
using CitizenSimulator.CitizenMind.Models;

namespace CitizenSimulator.CitizenMind.Services;

public class GoalSelector(CitizenMindService citizenMindService)
{
    public GoalDefinition? SelectGoals(MindComponent mindComponent)
    {
        Dictionary<GoalDefinition, double> eligibleGoals = new Dictionary<GoalDefinition, double>();
        Dictionary<GoalDefinition, int> emergencyGoals = new Dictionary<GoalDefinition, int>();
        foreach (var goal in citizenMindService.Goals)
        {
            if (!goal.Eligible.Evaluate(citizenMindService, mindComponent))
            {
                continue;
            }
            if (goal.Emergency != null && goal.Emergency.When.Evaluate(citizenMindService, mindComponent))
            {
                emergencyGoals.Add(goal, goal.Emergency.Priority);
                continue;
            }
            eligibleGoals.Add(goal, goal.Utility.Evaluate(citizenMindService, mindComponent));
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