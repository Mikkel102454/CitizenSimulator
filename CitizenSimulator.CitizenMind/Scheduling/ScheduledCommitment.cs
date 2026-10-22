using CitizenSimulator.CitizenMind.Planning;

namespace CitizenSimulator.CitizenMind.Scheduling;

/// <summary>A one-off goal to fulfil within an absolute game-time window.</summary>
public sealed class ScheduledCommitment
{
    public required string Id { get; init; }
    public required string GoalId { get; init; }
    public required TimeWindow Window { get; init; }
    public int Priority { get; init; }
    /// <summary>Replaces the goal's desired conditions with an occurrence-specific threshold when supplied.</summary>
    public CommitmentTarget? Target { get; init; }

    /// <summary>
    /// How early this goal may displace ordinary activities. Travel and preparation
    /// remain actions in the goal's plan; choose a lead time that allows them.
    /// </summary>
    public TimeSpan PreparationTime { get; init; }

    internal bool Matches(ScheduledCommitment other)
    {
        return SameOccurrence(other) && Priority == other.Priority && PreparationTime == other.PreparationTime;
    }

    internal bool SameOccurrence(ScheduledCommitment other)
    {
        return Id == other.Id && GoalId == other.GoalId && Window.Start == other.Window.Start
            && Window.End == other.Window.End && Target == other.Target;
    }
}

/// <summary>An occurrence-specific completion threshold, observed from the domain.</summary>
public sealed record CommitmentTarget(string Fact, double Minimum);
