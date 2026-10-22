using System.Globalization;
using CitizenSimulator.Calender.Services.Abstractions;

namespace CitizenSimulator.Business.Models;

public static class BusinessTime
{
    public static TimeSpan ShiftEnd(IEnumerable<OpeningHours> hours, TimeSpan instant, ICalenderService calendar)
    {
        var windows = GetOpeningWindows(hours, instant, instant + TimeSpan.FromDays(2), calendar);
        return windows.First(window => window.Start == instant).End;
    }

    public static bool IsOpen(IEnumerable<OpeningHours> hours, TimeSpan instant, ICalenderService calendar)
    {
        var day = calendar.GetDayOfWeek(instant);
        var timeOfDay = TimeOnly.FromTimeSpan(TimeSpan.FromTicks(instant.Ticks % TimeSpan.TicksPerDay));
        return hours.Any(openingHours => openingHours.IsOpen(day, timeOfDay));
    }

    public static decimal OverlapMinutes(
        IEnumerable<OpeningHours> hours,
        TimeSpan from,
        TimeSpan to,
        ICalenderService calendar)
    {
        return GetOpeningWindows(hours, from, to, calendar)
            .Sum(window => (window.End - window.Start).Ticks / (decimal)TimeSpan.TicksPerMinute);
    }

    public static decimal WorkingMinutes(
        IEnumerable<OpeningHours> shifts,
        IEnumerable<OpeningHours> businessHours,
        TimeSpan from,
        TimeSpan to,
        ICalenderService calendar)
    {
        var shiftWindows = GetOpeningWindows(shifts, from, to, calendar);
        var businessWindows = GetOpeningWindows(businessHours, from, to, calendar);
        long workingTicks = 0;

        foreach (var shiftWindow in shiftWindows)
        {
            foreach (var businessWindow in businessWindows)
            {
                long overlapStart = Math.Max(shiftWindow.Start.Ticks, businessWindow.Start.Ticks);
                long overlapEnd = Math.Min(shiftWindow.End.Ticks, businessWindow.End.Ticks);
                workingTicks += Math.Max(0, overlapEnd - overlapStart);
            }
        }

        return workingTicks / (decimal)TimeSpan.TicksPerMinute;
    }

    private static List<(TimeSpan Start, TimeSpan End)> GetOpeningWindows(
        IEnumerable<OpeningHours> hours,
        TimeSpan from,
        TimeSpan to,
        ICalenderService calendar)
    {
        if (to <= from)
            return [];

        var intervals = new List<(long Start, long End)>();
        // Include the previous day because an overnight window can continue into this interval.
        long firstDay = Math.Max(0, from.Ticks / TimeSpan.TicksPerDay - 1);
        long lastDay = to.Ticks / TimeSpan.TicksPerDay;

        for (long day = firstDay; day <= lastDay; day++)
        {
            var midnight = TimeSpan.FromTicks(day * TimeSpan.TicksPerDay);
            foreach (var openingHours in hours)
            {
                string dayName = calendar.GetDayOfWeek(midnight).ToString();
                if (!openingHours.Days.Contains(dayName, StringComparer.OrdinalIgnoreCase)
                    || !TimeOnly.TryParseExact(openingHours.Opens, "HH:mm", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var opens)
                    || !TimeOnly.TryParseExact(openingHours.Closes, "HH:mm", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var closes)
                    || opens == closes)
                    continue;

                long start = Math.Max(midnight.Ticks + opens.Ticks, from.Ticks);
                long overnightTicks = closes < opens ? TimeSpan.TicksPerDay : 0;
                long end = Math.Min(midnight.Ticks + closes.Ticks + overnightTicks, to.Ticks);
                if (end > start)
                    intervals.Add((start, end));
            }
        }

        var merged = new List<(TimeSpan Start, TimeSpan End)>();
        foreach (var interval in intervals.OrderBy(interval => interval.Start))
        {
            if (merged.Count > 0 && interval.Start <= merged[^1].End.Ticks)
            {
                var previous = merged[^1];
                var end = TimeSpan.FromTicks(Math.Max(interval.End, previous.End.Ticks));
                merged[^1] = (previous.Start, end);
            }
            else
            {
                merged.Add((TimeSpan.FromTicks(interval.Start), TimeSpan.FromTicks(interval.End)));
            }
        }

        return merged;
    }
}
