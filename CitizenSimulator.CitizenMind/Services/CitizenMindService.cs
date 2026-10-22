using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.CitizenMind.Configuration;
using CitizenSimulator.CitizenMind.Definitions;
using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Models.Abstractions;
using CitizenSimulator.CitizenMind.Planning;
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
    
    internal readonly Dictionary<string, CandidateProviderDescriptor> CandidateProviderDescriptors = [];

    internal readonly Dictionary<string, IElapsedTimePredictor> TimePredictors = [];
    
    private bool _registrationClosed;
    
    private void EnsureRegistrationOpen()
    {
        if (_registrationClosed)
            throw new InvalidOperationException("CitizenMind registration is closed after simulation has started.");
    }
    
    internal void CloseRegistration()
    {
        _registrationClosed = true;
    }

    public void RegisterTimePredictor(string id, IElapsedTimePredictor predictor)
    {
        EnsureRegistrationOpen();
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(predictor);

        if (!TimePredictors.TryAdd(id, predictor))
            throw new InvalidOperationException($"[CitizenMind] Duplicate time predictor id '{id}'.");
    }
    
    public void RegisterFact<T>(string id, Func<EntityRef, T> provider, bool canPredict = false)
    {
        EnsureRegistrationOpen();
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
        EnsureRegistrationOpen();
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        
        if(!Handler.TryAdd(id, handler))
            throw new Exception($"[CitizenMind] Duplicate handler id '{id}'");
    }
    
    public void RegisterGoal(GoalDefinition goal)
    {
        EnsureRegistrationOpen();
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentException.ThrowIfNullOrWhiteSpace(goal.Id);

        if (Goals.Values.Any(x => x.Id == goal.Id))
            throw new InvalidOperationException($"[CitizenMind] Duplicate goal id '{goal.Id}'");

        Goals.Add(goal.Id, goal);
    }
    
    public void RegisterAction(ActionDefinition action)
    {
        EnsureRegistrationOpen();
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(action.Id);

        if (Actions.Values.Any(x => x.Id == action.Id))
            throw new InvalidOperationException($"[CitizenMind] Duplicate action id '{action.Id}'");

        action.ResolvedParameters = ParameterParser.ParseAll(action.Parameters, action.Id);
        Actions.Add(action.Id, action);
    }
    
    public EvaluationContext CreateEvaluationContext(EntityRef citizen, TimeSpan gameTime)
    {
        var facts = new Dictionary<string, object?>();

        foreach (var (id, provider) in Facts)
        {
            facts.Add(id, provider.DynamicInvoke(citizen));
        }

        return new EvaluationContext
        {
            Facts = facts,
            GameTime = gameTime
        };
    }
    
    public void RegisterCandidateProvider(CandidateProviderDescriptor descriptor, Func<EntityRef, IActionCandidateProvider> factory)
    {
        EnsureRegistrationOpen();
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Id);

        if (descriptor.BindingTypes == null)
            throw new InvalidOperationException($"Provider '{descriptor.Id}': binding types are required.");

        if (CandidateProviderFactories.ContainsKey(descriptor.Id) || CandidateProviderDescriptors.ContainsKey(descriptor.Id))
        {
            throw new InvalidOperationException($"Duplicate candidate provider '{descriptor.Id}'.");
        }

        var bindingTypes = new Dictionary<string, Type>();

        foreach (var (name, type) in descriptor.BindingTypes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(type);

            bindingTypes.Add(name, type);
        }

        var registeredDescriptor = new CandidateProviderDescriptor
        {
            Id = descriptor.Id,
            BindingTypes = new System.Collections.ObjectModel.ReadOnlyDictionary<string, Type>(bindingTypes)
        };

        CandidateProviderFactories.Add(descriptor.Id, factory);
        CandidateProviderDescriptors.Add(descriptor.Id, registeredDescriptor);
    }
    
    public IReadOnlyDictionary<string, CandidateProviderSnapshot> CreateCandidateProviders(EntityRef citizen, IEnumerable<ActionDefinition> definitions)
    {
        var providers = new Dictionary<string, CandidateProviderSnapshot>();

        var ids = definitions.Select(action => action.CandidateProvider).OfType<string>()
            .Distinct().OrderBy(id => id, StringComparer.Ordinal);

        foreach (var id in ids)
        {
            if (!CandidateProviderFactories.TryGetValue(id, out var factory))
                throw new InvalidOperationException($"Unknown candidate provider '{id}'.");

            var provider = factory(citizen) ?? throw new InvalidOperationException($"Candidate provider factory '{id}' returned null.");

            
            if (!CandidateProviderDescriptors.TryGetValue(id, out var descriptor))
                throw new InvalidOperationException($"Missing descriptor for candidate provider '{id}'.");

            providers.Add(id, new CandidateProviderSnapshot
            {
                Descriptor = descriptor,
                Provider = provider
            });
        }

        return providers;
    }
}
