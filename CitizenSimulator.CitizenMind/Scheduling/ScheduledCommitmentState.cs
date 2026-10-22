namespace CitizenSimulator.CitizenMind.Scheduling;

public sealed class ScheduledCommitmentState
{
    public ScheduledCommitment Commitment { get; }
    public CommitmentStatus Status { get; internal set; }

    /// <summary>Null for a manually supplied commitment.</summary>
    public string? SourceRuleId { get; }
    public long? OccurrenceDay { get; }

    internal ScheduledCommitmentState(ScheduledCommitment commitment,
        string? sourceRuleId = null, long? occurrenceDay = null)
    {
        Commitment = commitment;
        SourceRuleId = sourceRuleId;
        OccurrenceDay = occurrenceDay;
    }
}
