namespace CitizenSimulator.CitizenMind.Scheduling;

public sealed class ScheduledCommitmentState
{
    public ScheduledCommitment Commitment { get; }
    public CommitmentStatus Status { get; internal set; }

    /// <summary>The daily rule that generated this occurrence, or null for one-off commitments.</summary>
    public string? SourceRuleId { get; }
    public long? OccurrenceDay { get; }
    /// <summary>The publishing mod's source ID, or null for manual/daily commitments.</summary>
    public string? SourceId { get; }

    internal ScheduledCommitmentState(ScheduledCommitment commitment, string? sourceRuleId = null,
        long? occurrenceDay = null, string? sourceId = null)
    {
        Commitment = commitment;
        SourceRuleId = sourceRuleId;
        OccurrenceDay = occurrenceDay;
        SourceId = sourceId;
    }
}
