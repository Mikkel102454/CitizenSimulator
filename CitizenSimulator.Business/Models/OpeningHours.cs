namespace CitizenSimulator.Business.Models;

public class OpeningHours
{
    public List<string> Days { get; init; } = [];
    public string Opens { get; init; } = "";
    public string Closes { get; init; } = "";

    public bool IsOpen(DayOfWeek day, TimeOnly time)
    {
        if (!TimeOnly.TryParseExact(Opens, "HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var opens)
            || !TimeOnly.TryParseExact(Closes, "HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var closes))
            return false;

        // equal endpoints represent a closed window and not 24-hour opening.
        if (opens == closes)
            return false;

        bool opensToday = Days.Contains(day.ToString(), StringComparer.OrdinalIgnoreCase);
        if (opens < closes)
            return opensToday && time >= opens && time < closes;

        var previousDay = (DayOfWeek)(((int)day + 6) % 7);
        bool openedYesterday = Days.Contains(previousDay.ToString(), StringComparer.OrdinalIgnoreCase);
        return (opensToday && time >= opens) || (openedYesterday && time < closes);
    }
}
