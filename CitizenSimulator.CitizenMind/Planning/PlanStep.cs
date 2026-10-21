using CitizenSimulator.CitizenMind.Models;

namespace CitizenSimulator.CitizenMind.Planning;

public sealed class PlanStep
{
    public required ActionCandidate Candidate { get; init; }
    
    public double DurationMinutes { get; init; }
}