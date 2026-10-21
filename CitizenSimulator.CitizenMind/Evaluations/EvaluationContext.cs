namespace CitizenSimulator.CitizenMind.Evaluations;

public class EvaluationContext
{
    public required Dictionary<string, object?> Facts { get; init; }
    
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();
    public Dictionary<string, double> Calculated { get; init; } = [];

    public Dictionary<string, object?> Before { get; init; } = [];
}