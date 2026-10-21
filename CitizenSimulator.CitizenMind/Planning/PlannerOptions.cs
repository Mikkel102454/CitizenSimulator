namespace CitizenSimulator.CitizenMind.Planning;

public sealed class PlannerOptions
{
    public int MaxDepth { get; init; } = 8;
    public int MaxExpansions { get; init; } = 256;
    
    public int MaxCandidatesPerAction { get; init; } = 32;
}