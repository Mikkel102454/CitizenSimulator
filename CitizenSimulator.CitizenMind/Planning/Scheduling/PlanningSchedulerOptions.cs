namespace CitizenSimulator.CitizenMind.Planning.Scheduling;

public sealed class PlanningSchedulerOptions
{
    /// <summary>
    /// Global work cap, including nested forecasts, setup and fresh plan replay.
    /// Atomic validation must fit within this cap or the request exceeds its budget.
    /// Provider callbacks remain synchronous; this is not a wall-clock time limit.
    /// </summary>
    public int MaxExpansionsPerTick { get; init; } = 256;
    public int ExpansionsPerSlice { get; init; } = 8;
}
