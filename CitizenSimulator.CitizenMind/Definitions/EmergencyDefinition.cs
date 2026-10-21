namespace CitizenSimulator.CitizenMind.Models;

public sealed class EmergencyDefinition
{
    public required ExpressionDefinition When { get; init; }

    public required int Priority { get; init; }
}