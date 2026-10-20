using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Models.Abstractions;
using CitizenSimulator.CitizenMind.Services.Abstractions;

namespace CitizenSimulator.CitizenMind.Services;

public class CitizenMindService : ICitizenMindService
{
    internal readonly Dictionary<string, Delegate> Facts = new(StringComparer.Ordinal);

    internal readonly Dictionary<string, IMindActionHandler> Handler = new(StringComparer.Ordinal);
    internal readonly List<GoalDefinition> Goals = [];
    public void RegisterFact<T>(string id, Func<EntityRef, T> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        
        if(!Facts.TryAdd(id, provider))
            throw new Exception($"[CitizenMind] Duplicate fact id '{id}'");
    }

    public void RegisterHandler(string id, IMindActionHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        
        if(!Handler.TryAdd(id, handler))
            throw new Exception($"[CitizenMind] Duplicate handler id '{id}'");
    }
    
    public void RegisterGoal(GoalDefinition goal)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentException.ThrowIfNullOrWhiteSpace(goal.Id);

        if (Goals.Any(x => x.Id == goal.Id))
            throw new InvalidOperationException($"[CitizenMind] Duplicate goal id '{goal.Id}'");

        Goals.Add(goal);
    }
}