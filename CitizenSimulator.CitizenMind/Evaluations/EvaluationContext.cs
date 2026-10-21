namespace CitizenSimulator.CitizenMind.Evaluations;

public class EvaluationContext
{
    public required Dictionary<string, object?> Facts { get; init; }
    public Dictionary<string, double> Parameters { get; init; } = [];
    public Dictionary<string, double> Calculated { get; init; } = [];
}