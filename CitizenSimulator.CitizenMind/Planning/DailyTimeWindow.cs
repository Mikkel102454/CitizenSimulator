namespace CitizenSimulator.CitizenMind.Planning;

public sealed class DailyTimeWindow
{
    public TimeSpan Start { get; }
    public TimeSpan End { get; }

    public DailyTimeWindow(TimeSpan start, TimeSpan end)
    {
        var day = TimeSpan.FromDays(1);

        if (start < TimeSpan.Zero || start >= day)
            throw new ArgumentOutOfRangeException(nameof(start));

        if (end < TimeSpan.Zero || end > day || end == start)
            throw new ArgumentOutOfRangeException(nameof(end));

        Start = start;
        End = end;
    }

    public TimeWindow GetWindowForDay(long dayIndex)
    {
        var midnight = TimeSpan.FromTicks(checked(dayIndex * TimeSpan.TicksPerDay));

        var start = midnight + Start;
        var end = midnight + End;

        if (End < Start)
            end += TimeSpan.FromDays(1);

        return new TimeWindow(start, end);
    }
    
    public bool CanFit(TimeSpan start, TimeSpan duration)
    {
        long dayIndex = start.Ticks / TimeSpan.TicksPerDay;

        return GetWindowForDay(dayIndex).CanFit(start, duration) || GetWindowForDay(dayIndex - 1).CanFit(start, duration);
    }
}