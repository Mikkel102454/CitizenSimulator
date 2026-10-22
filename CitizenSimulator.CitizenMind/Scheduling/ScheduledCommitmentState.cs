namespace CitizenSimulator.CitizenMind.Scheduling;

public sealed class ScheduledCommitmentState
{
    public ScheduledCommitment Commitment { get; }
    public CommitmentStatus Status { get; internal set; }

    internal ScheduledCommitmentState(ScheduledCommitment commitment)
    {
        Commitment = commitment;
    }
}
