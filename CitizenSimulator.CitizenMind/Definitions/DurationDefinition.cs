namespace CitizenSimulator.CitizenMind.Models;

public sealed class DurationDefinition
{
    public required string Unit { get; init; }
    
    public required ExpressionDefinition Estimate { get; init; }
}