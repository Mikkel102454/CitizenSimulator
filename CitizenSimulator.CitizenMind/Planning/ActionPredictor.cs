using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Models;

namespace CitizenSimulator.CitizenMind.Planning;

public class ActionPredictor
{
    public static EvaluationContext Predict(ActionDefinition action, EvaluationContext state, double durationMinutes)
    {
        // new copy
        Dictionary<string, object?> facts = new(state.Facts, state.Facts.Comparer);
        
        var context = new EvaluationContext
        {
            Facts = state.Facts,
            Parameters = action.Parameters,
            Calculated = new Dictionary<string, double>
            {
                ["durationMinutes"] = durationMinutes
            }
        };
        
        var targets = new HashSet<string>(facts.Comparer);

        foreach (var effect in action.Prediction)
        {
            if (!facts.ContainsKey(effect.Fact))
                throw new InvalidOperationException($"Unknown fact '{effect.Fact}'.");

            if (!targets.Add(effect.Fact))
                throw new InvalidOperationException($"Multiple effects target fact '{effect.Fact}'.");

            var value = ExpressionEvaluator.Resolve(context, effect.Value);

            var original = state.Facts[effect.Fact];

            switch (effect.Op)
            {
                case "set":
                    if (original is bool)
                    {
                        if (value is not bool)
                            throw new InvalidOperationException($"Prediction for '{effect.Fact}' requires a boolean.");

                        facts[effect.Fact] = value;
                    }
                    else
                    {
                        RequireNumber(original, effect.Fact);
                        facts[effect.Fact] = RequireNumber(value, effect.Fact);
                    }
                    break;

                case "add":
                case "subtract":
                {
                    double current = RequireNumber(original, effect.Fact);
                    double amount = RequireNumber(value, effect.Fact);

                    double result = effect.Op == "add" ? current + amount : current - amount;

                    if (!double.IsFinite(result))
                        throw new InvalidOperationException($"Prediction for '{effect.Fact}' produced a nonfinite result.");

                    facts[effect.Fact] = result;
                    break;
                }

                default:
                    throw new InvalidOperationException($"Unsupported prediction operation '{effect.Op}'. Valid operations are 'set', 'add', 'subtract'.");
            }
        }

        return new EvaluationContext
        {
            Facts = facts
        };
    }
    
    
    private static double RequireNumber(object? value, string fact)
    {
        if (value is not (int or float or double or decimal))
            throw new InvalidOperationException($"Prediction for '{fact}' requires a number.");

        double number = Convert.ToDouble(value);

        if (!double.IsFinite(number))
            throw new InvalidOperationException($"Prediction for '{fact}' requires a finite number.");

        return number;
    }
}