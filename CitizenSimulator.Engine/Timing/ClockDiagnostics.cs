namespace CitizenSimulator.Engine.Timing;

public readonly record struct ClockDiagnostics(
    double AchievedTicksPerSecond,
    TimeSpan AverageTickCost,
    TimeSpan MaximumTickCost,
    double PendingTicks,
    bool IsOverloaded);
