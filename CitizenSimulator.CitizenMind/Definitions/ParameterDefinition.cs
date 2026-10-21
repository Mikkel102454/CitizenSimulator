namespace CitizenSimulator.CitizenMind.Definitions;

public sealed class ParameterDefinition
{
    public required ParameterType Type { get; init; }
    
    public required string Value { get; init; }
}