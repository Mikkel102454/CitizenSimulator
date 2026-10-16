namespace CitizenSimulator.Abstractions.Timing;

// read only clock state
public interface ISimulationClock
{
    int TickRate { get; }
    ulong Tick { get; }
    TimeSpan Delta { get; }
    TimeSpan Elapsed { get; }
    bool IsPaused { get; }
    double Speed { get; }
    bool IsAdvancing { get; }
}
