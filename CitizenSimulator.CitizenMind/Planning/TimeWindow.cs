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
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);

        return start >= Start && start < End && duration <= End - start;
    }
}
