using CitizenSimulator.CitizenMind.Models;

namespace CitizenSimulator.CitizenMind.Planning;


public sealed class ActionCandidate
{
    public required string Id { get; init; }

    public required ActionDefinition Definition { get; init; }

    public IReadOnlyDictionary<string, object?> Bindings { get; init; } = new Dictionary<string, object?>();

    public IReadOnlyList<TimeWindow>? Availability { get; init; }

    public IReadOnlyList<DailyTimeWindow>? DailyAvailability { get; init; }

    public bool IsAvailable(TimeSpan start, TimeSpan duration)
    {
        bool absoluteAllowed = Availability == null || Availability.Any(window => window.CanFit(start, duration));
        bool dailyAllowed = DailyAvailability == null || DailyAvailability.Any(window => window.CanFit(start, duration));

        return absoluteAllowed && dailyAllowed;
    }

    public IReadOnlyList<TimeSpan> GetUpcomingOpeningTimes(TimeSpan now)
    {
        var times = new List<TimeSpan>();

        if (Availability != null)
        {
            foreach (var window in Availability)
            {
                if (window.Start > now)
                    times.Add(window.Start);
            }
        }

        if (DailyAvailability != null)
        {
            long today = now.Ticks / TimeSpan.TicksPerDay;

            foreach (var daily in DailyAvailability)
            {
                var todayWindow = daily.GetWindowForDay(today);
                var tomorrowWindow = daily.GetWindowForDay(today + 1);

                if (todayWindow.Start > now)
                    times.Add(todayWindow.Start);

                times.Add(tomorrowWindow.Start);
            }
        }

        return times.Distinct().OrderBy(time => time).ToList();
    }
}
