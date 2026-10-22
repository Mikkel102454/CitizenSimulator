using CitizenSimulator.CitizenMind.Definitions;
using YamlDotNet.Serialization;

namespace CitizenSimulator.CitizenMind.Models;

public sealed class ActionDefinition
{
    [YamlIgnore]
    public string? YamlSource { get; internal set; }

    public required string Id { get; init; }
    
    public required string Handler { get; init; }

    public Dictionary<string, ParameterDefinition> Parameters { get; init; } = [];
    
    public List<ExpressionDefinition> Preconditions { get; init; } = [];

    // so yaml parser dosnt try to put things in here
    [YamlIgnore]
    public IReadOnlyDictionary<string, object?> ResolvedParameters
    { get; internal set; } = new Dictionary<string, object?>();
    public required DurationDefinition Duration { get; init; }
    
    public required List<PredictionEffectDefinition> Prediction { get; init; }
    
    public required List<ExpressionDefinition> Completion { get; init; }
    
    public required CostDefinition Cost { get; init; }
    
    public ExecutionDefinition Execution { get; init; } = new ExecutionDefinition();
    
    public string? CandidateProvider { get; init; }
}
