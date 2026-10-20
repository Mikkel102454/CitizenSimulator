using CitizenSimulator.CitizenMind.Components;
using CitizenSimulator.CitizenMind.Services;

namespace CitizenSimulator.CitizenMind.Models;


public sealed class UtilityDefinition
{
    public double Bias { get; init; } = 1.0;

    public required List<ConsiderationDefinition> Considerations { get; init; }

    public required List<string> Actions { get; init; }

    public double Evaluate(CitizenMindService service, MindComponent mind)
    {
        if (!double.IsFinite(Bias) || Bias < 0)
            throw new InvalidOperationException("Bias must be finite and nonnegative.");

        double weightedSum = 0;
        double totalWeight = 0;

        foreach (var consideration in Considerations)
        {
            double weight = consideration.Weight;

            if (!double.IsFinite(weight) || weight < 0)
                throw new InvalidOperationException("Weights must be finite and nonnegative.");

            if (weight == 0)
                continue;

            double score = consideration.Evaluate(service, mind);

            weightedSum += score * weight;
            totalWeight += weight;
        }

        if (totalWeight == 0)
            throw new InvalidOperationException("Utility requires at least one positive weight.");

        return Math.Clamp(Bias * (weightedSum / totalWeight), 0, 1);
    }
}