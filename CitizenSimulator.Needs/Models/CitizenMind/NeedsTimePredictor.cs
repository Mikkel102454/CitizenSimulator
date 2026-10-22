using CitizenSimulator.CitizenMind.Planning.Abstractions;

namespace CitizenSimulator.Needs.Models.CitizenMind;

public sealed class NeedsTimePredictor : IElapsedTimePredictor
{
    public IReadOnlyDictionary<string, object?> Predict(IReadOnlyDictionary<string, object?> facts, TimeSpan gameTime, TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed));

        var updates = new Dictionary<string, object?>();

        if (elapsed == TimeSpan.Zero)
            return updates;

        void PredictNeed(string fact, double changePerDay)
        {
            if (!facts.TryGetValue(fact, out var value))
                throw new InvalidOperationException($"Missing needs fact '{fact}'.");

            if (value == null)
                return;

            if (value is not double current || !double.IsFinite(current))
                throw new InvalidOperationException($"Needs fact '{fact}' must be a finite double.");

            updates.Add(fact, NeedsDynamics.Advance(current, changePerDay, elapsed));
        }

        PredictNeed("needs.thirst", NeedsDynamics.ThirstChangePerDay);
        PredictNeed("needs.hunger", NeedsDynamics.HungerChangePerDay);
        PredictNeed("needs.energy", NeedsDynamics.EnergyChangePerDay);

        return updates;
    }
}
