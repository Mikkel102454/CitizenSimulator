namespace CitizenSimulator.CitizenMind.Models;

public sealed class ActionDefinition
{
    public required string Id { get; init; }
    
    public required string Handler { get; init; }
    
    public Dictionary<string, double> Parameters { get; init; } = [];
    
    public List<ExpressionDefinition> Preconditions { get; init; } = [];
    
    public required DurationDefinition Duration { get; init; }
    
    public required List<PredictionEffectDefinition> Prediction { get; init; }
    
    public required List<ExpressionDefinition> Completion { get; init; }
    
    public required CostDefinition Cost { get; init; }
    
    public ExecutionDefinition Execution { get; init; } = new ExecutionDefinition();
}