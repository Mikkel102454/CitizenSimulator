using CitizenSimulator.CitizenMind.Components;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Services;

namespace CitizenSimulator.CitizenMind.Evaluations;

public class UtilityEvaluator
{
    public static double Evaluate(EvaluationContext context, UtilityDefinition utility)
    {
        if (!double.IsFinite(utility.Bias) || utility.Bias < 0)
            throw new InvalidOperationException("Bias must be finite and nonnegative.");

        double weightedSum = 0;
        double totalWeight = 0;

        foreach (var consideration in utility.Considerations)
        {
            double weight = consideration.Weight;

            if (!double.IsFinite(weight) || weight < 0)
                throw new InvalidOperationException("Weights must be finite and nonnegative.");

            if (weight == 0)
                continue;

            double score = ConsiderationEvaluator.Evaluate(context, consideration);

            weightedSum += score * weight;
            totalWeight += weight;
        }

        if (totalWeight == 0)
            throw new InvalidOperationException("Utility requires at least one positive weight.");

        return Math.Clamp(utility.Bias * (weightedSum / totalWeight), 0, 1);
    }
}