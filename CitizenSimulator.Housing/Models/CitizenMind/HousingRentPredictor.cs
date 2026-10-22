using CitizenSimulator.CitizenMind.Planning.Abstractions;
using CitizenSimulator.Vault.Models.CitizenMind;

namespace CitizenSimulator.Housing.Models.CitizenMind;

public sealed class HousingRentPredictor : IElapsedTimePredictor
{
    public IReadOnlyDictionary<string, object?> Predict(IReadOnlyDictionary<string, object?> facts, TimeSpan gameTime, TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed));

        if (facts.GetValueOrDefault(AccountFacts.Balance) is not decimal balance
            || facts.GetValueOrDefault(HousingActions.DailyRentFact) is not decimal rent || rent <= 0 || elapsed == TimeSpan.Zero)
            return new Dictionary<string, object?>();

        decimal days = elapsed.Ticks / (decimal)TimeSpan.TicksPerDay;
        decimal charge = days > 1 && rent > decimal.MaxValue / days ? decimal.MaxValue : days * rent;
        decimal remaining = Math.Max(0, balance - charge);
        return new Dictionary<string, object?> { [AccountFacts.Balance] = remaining };
    }
}
