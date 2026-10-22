using System.Collections.ObjectModel;
using CitizenSimulator.CitizenMind.Definitions;
using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Planning.Abstractions;

namespace CitizenSimulator.CitizenMind.Planning;

internal static class ElapsedTimePredictor
{
    public static EvaluationContext Predict(
        EvaluationContext state,
        TimeSpan elapsed,
        IReadOnlyDictionary<string, FactDescriptor> descriptors,
        IReadOnlyDictionary<string, IElapsedTimePredictor>? predictors)
    {
        if (elapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed));

        var gameTime = state.GameTime + elapsed;
        var facts = new Dictionary<string, object?>(state.Facts, state.Facts.Comparer);

        if (elapsed > TimeSpan.Zero && predictors is { Count: > 0 })
        {
            var snapshot = new ReadOnlyDictionary<string, object?>(facts);
            var updates = new Dictionary<string, object?>(facts.Comparer);

            foreach (var (id, predictor) in predictors)
            {
                var changes = predictor.Predict(snapshot, state.GameTime, elapsed)
                    ?? throw new InvalidOperationException($"Time predictor '{id}' returned null updates.");

                foreach (var (fact, value) in changes)
                {
                    if (!descriptors.TryGetValue(fact, out var descriptor) || !facts.ContainsKey(fact))
                        throw new InvalidOperationException($"Time predictor '{id}' updates unknown fact '{fact}'.");

                    if (!descriptor.CanPredict)
                        throw new InvalidOperationException($"Time predictor '{id}' cannot update read-only fact '{fact}'.");

                    if (!updates.TryAdd(fact, ActionPredictor.RequireValue(value, descriptor)))
                        throw new InvalidOperationException($"Multiple time predictors update fact '{fact}'.");
                }
            }

            foreach (var (fact, value) in updates)
                facts[fact] = value;
        }

        return new EvaluationContext
        {
            Facts = facts,
            GameTime = gameTime
        };
    }
}
