using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Models;

namespace CitizenSimulator.CitizenMind.Planning;

internal sealed class SearchNode
{
    public required EvaluationContext State { get; init; }
    
    public SearchNode? Parent { get; init; }
    
    public ActionCandidate? Candidate { get; init; }
    
    public double DurationMinutes { get; init; }
    
    public double TotalCost { get; init; }
    
    public int Depth { get; init; }
}