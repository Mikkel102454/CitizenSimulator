namespace CitizenSimulator.Abstractions.Timing;

/// <summary>Read-only clock state. Clock controls are owned by the host.</summary>
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
