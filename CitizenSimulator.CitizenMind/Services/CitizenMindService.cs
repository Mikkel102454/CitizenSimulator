using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.CitizenMind.Configuration;
using CitizenSimulator.CitizenMind.Definitions;
using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Models.Abstractions;
using CitizenSimulator.CitizenMind.Planning.Abstractions;
using CitizenSimulator.CitizenMind.Services.Abstractions;

namespace CitizenSimulator.CitizenMind.Services;

public class CitizenMindService : ICitizenMindService
{
    internal readonly Dictionary<string, Delegate> Facts = [];

    internal readonly Dictionary<string, FactDescriptor> FactDescriptors = [];
    
    internal readonly Dictionary<string, IMindActionHandler> Handler = [];
    
    internal readonly Dictionary<string, GoalDefinition> Goals = [];
    
    internal readonly Dictionary<string, ActionDefinition> Actions = [];
    
    internal readonly Dictionary<string, Func<EntityRef, IActionCandidateProvider>> CandidateProviderFactories = [];
    public void RegisterFact<T>(string id, Func<EntityRef, T> provider, bool canPredict = false)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        
        if (Facts.ContainsKey(id) || FactDescriptors.ContainsKey(id))
            throw new InvalidOperationException($"[CitizenMind] Duplicate fact id '{id}'.");

        var descriptor = new FactDescriptor
        {
            Id = id,
            ValueType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T),
            CanPredict = canPredict
        };

        Facts.Add(id, provider);
        FactDescriptors.Add(id, descriptor);
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

        if (Goals.Values.Any(x => x.Id == goal.Id))
            throw new InvalidOperationException($"[CitizenMind] Duplicate goal id '{goal.Id}'");

        Goals.Add(goal.Id, goal);
    }
    
    public void RegisterAction(ActionDefinition action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(action.Id);

        if (Actions.Values.Any(x => x.Id == action.Id))
            throw new InvalidOperationException($"[CitizenMind] Duplicate action id '{action.Id}'");

        action.ResolvedParameters = ParameterParser.ParseAll(action.Parameters, action.Id);
        Actions.Add(action.Id, action);
    }
    
    public EvaluationContext CreateEvaluationContext(EntityRef citizen)
    {
        var facts = new Dictionary<string, object?>();

        foreach (var (id, provider) in Facts)
        {
            facts.Add(id, provider.DynamicInvoke(citizen));
        }

        return new EvaluationContext
        {
            Facts = facts
        };
    }
    
    public void RegisterCandidateProvider(string id, Func<EntityRef, IActionCandidateProvider> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(factory);

        if (!CandidateProviderFactories.TryAdd(id, factory))
            throw new InvalidOperationException($"[CitizenMind] Duplicate candidate provider '{id}'.");
    }
    
    public IReadOnlyDictionary<string, IActionCandidateProvider> CreateCandidateProviders(EntityRef citizen, IEnumerable<ActionDefinition> definitions)
    {
        var providers = new Dictionary<string, IActionCandidateProvider>();

        var ids = definitions.Select(action => action.CandidateProvider).OfType<string>()
            .Distinct().OrderBy(id => id, StringComparer.Ordinal);

        foreach (var id in ids)
        {
            if (!CandidateProviderFactories.TryGetValue(id, out var factory))
                throw new InvalidOperationException($"Unknown candidate provider '{id}'.");

            var provider = factory(citizen) ?? throw new InvalidOperationException($"Candidate provider factory '{id}' returned null.");

            providers.Add(id, provider);
        }

        return providers;
    }
}