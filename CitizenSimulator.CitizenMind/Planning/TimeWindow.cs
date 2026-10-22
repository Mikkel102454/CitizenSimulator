namespace CitizenSimulator.CitizenMind.Planning;

public sealed class TimeWindow
{
    public TimeSpan Start { get; }
    public TimeSpan End { get; }

    public TimeWindow(TimeSpan start, TimeSpan end)
    {
        if (end <= start)
            throw new ArgumentException("End must be after start.");

        Start = start;
        End = end;
    }

    public bool CanFit(TimeSpan start, TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration));

        return start >= Start &&
               start < End &&
               duration <= End - start;
    }
}
