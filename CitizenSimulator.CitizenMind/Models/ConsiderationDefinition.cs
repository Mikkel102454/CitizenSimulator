using CitizenSimulator.CitizenMind.Components;
using CitizenSimulator.CitizenMind.Services;

namespace CitizenSimulator.CitizenMind.Models;

public sealed class ConsiderationDefinition
{
    public required ExpressionDefinition Input { get; init; }

    public double Weight { get; init; } = 1.0;
    public required List<double[]> Curve { get; init; }
    
    public double Evaluate(CitizenMindService service, MindComponent mind)
    {
        object? value = Input?.Resolve(service, mind);
        
        if (value is not (int or float or double or decimal))
            throw new InvalidOperationException("Consideration input must be numeric.");

        double input = Convert.ToDouble(value);
        
        return CalculateCurve(input);
    }
    
    private double CalculateCurve(double input)
    {
        if (!double.IsFinite(input))
            throw new ArgumentException("Input must be finite.");
        
        if (Curve.Count == 0)
            throw new InvalidOperationException("Curve cannot be empty.");
        
        if (input <= Curve[0][0])
            return Curve[0][1];

        for (int i = 1; i < Curve.Count; i++)
        {
            double[] previous = Curve[i - 1];
            double[] next = Curve[i];

            if (input <= next[0])
            {
                double fraction = (input - previous[0]) / (next[0] - previous[0]);

                return previous[1] + fraction * (next[1] - previous[1]);
            }
        }

        return Curve[^1][1];
    }
}