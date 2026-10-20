namespace CitizenSimulator.CitizenMind.Models;

public sealed class GoalDefinition
{
    public required string Id { get; init; }

    public required ExpressionDefinition Eligible { get; init; }

    public EmergencyDefinition? Emergency { get; init; }

    // All conditions must be true for the goal to be satisfied.
    public required List<ExpressionDefinition> Desired { get; init; }

    public required UtilityDefinition Utility { get; init; }
}