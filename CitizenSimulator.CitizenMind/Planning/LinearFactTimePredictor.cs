using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Planning.Abstractions;

namespace CitizenSimulator.CitizenMind.Planning;

/// <summary>Predicts a double fact changing at a constant rate per game day.</summary>
public sealed class LinearFactTimePredictor : IElapsedTimePredictor
{
    private readonly string _fact;
    private readonly double _changePerDay;
    private readonly double _minimum;
    private readonly double _maximum;

    public LinearFactTimePredictor(string fact, double changePerDay, double minimum = double.NegativeInfinity, double maximum = double.PositiveInfinity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fact);
        if (!double.IsFinite(changePerDay))
            throw new ArgumentOutOfRangeException(nameof(changePerDay));

        if (double.IsNaN(minimum) || double.IsPositiveInfinity(minimum))
            throw new ArgumentOutOfRangeException(nameof(minimum));

        if (double.IsNaN(maximum) || double.IsNegativeInfinity(minimum) || maximum < minimum)
            throw new ArgumentOutOfRangeException(nameof(maximum));

        _fact = fact;
        _changePerDay = changePerDay;
        _minimum = minimum;
        _maximum = maximum;
    }

    public IReadOnlyDictionary<string, object?> Predict(IReadOnlyDictionary<string, object?> facts, TimeSpan gameTime, TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed));

        if (!facts.TryGetValue(_fact, out var current))
            throw new InvalidOperationException($"Unknown fact '{_fact}'.");

        if (current == null || elapsed == TimeSpan.Zero)
            return new Dictionary<string, object?>();

        double value = NumericValues.RequireNumber(current, _fact) + _changePerDay * elapsed.TotalDays;

        if (!double.IsFinite(value))
            throw new InvalidOperationException($"Time prediction for '{_fact}' produced a nonfinite result.");

        return new Dictionary<string, object?>
        {
            [_fact] = Math.Clamp(value, _minimum, _maximum)
        };
    }
}
