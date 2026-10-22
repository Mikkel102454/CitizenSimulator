using CitizenSimulator.CitizenMind.Planning;

namespace CitizenSimulator.CitizenMind.Scheduling;

/// <summary>A one-off goal to fulfil within an absolute game-time window.</summary>
public sealed class ScheduledCommitment
{
    public required string Id { get; init; }
    public required string GoalId { get; init; }
    public required TimeWindow Window { get; init; }
    public int Priority { get; init; }

    /// <summary>
    /// How early this goal may displace ordinary activities. Travel and preparation
    /// remain actions in the goal's plan; choose a lead time that allows them.
    /// </summary>
    public TimeSpan PreparationTime { get; init; }
}
