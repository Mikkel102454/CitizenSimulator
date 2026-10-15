namespace CitizenSimulator.Abstractions.Timing;

// destination time for a completed tick
public readonly record struct SimulationTime(ulong Tick, TimeSpan Delta, TimeSpan Elapsed);
