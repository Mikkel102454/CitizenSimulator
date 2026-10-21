using CitizenSimulator.CitizenMind.Components;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Services;

namespace CitizenSimulator.CitizenMind.Evaluations;

public class ConsiderationEvaluator
{
    public static double Evaluate(EvaluationContext context, ConsiderationDefinition definition)
    {
        object? value = ExpressionEvaluator.Resolve(context, definition.Input);
        
        if (value is not (int or float or double or decimal))
            throw new InvalidOperationException("Consideration input must be numeric.");

        double input = Convert.ToDouble(value);
        
        return CalculateCurve(input, definition);
    }
    
    private static double CalculateCurve(double input, ConsiderationDefinition definition)
    {
        if (!double.IsFinite(input))
            throw new ArgumentException("Input must be finite.");
        
        if (definition.Curve.Count == 0)
            throw new InvalidOperationException("Curve cannot be empty.");
        
        if (input <= definition.Curve[0][0])
            return definition.Curve[0][1];

        for (int i = 1; i < definition.Curve.Count; i++)
        {
            double[] previous = definition.Curve[i - 1];
            double[] next = definition.Curve[i];

            if (input <= next[0])
            {
                double fraction = (input - previous[0]) / (next[0] - previous[0]);

                return previous[1] + fraction * (next[1] - previous[1]);
            }
        }

        return definition.Curve[^1][1];
    }
}