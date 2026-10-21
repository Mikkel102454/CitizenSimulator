using CitizenSimulator.CitizenMind.Models;

namespace CitizenSimulator.CitizenMind.Planning;

public sealed class PlanStep
{
    public required ActionDefinition Action { get; init; }
    
    public double DurationMinutes { get; init; }
}