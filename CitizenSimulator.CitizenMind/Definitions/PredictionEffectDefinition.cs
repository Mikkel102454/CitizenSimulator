namespace CitizenSimulator.CitizenMind.Models;

public sealed class PredictionEffectDefinition
{
    public required string Fact  { get; init; }
    
    public required string Op { get; init; }
    
    public required ExpressionDefinition Value { get; init; }
}