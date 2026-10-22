using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Scheduling;

namespace CitizenSimulator.CitizenMind.Services;

public sealed record GoalSelection(
    GoalDefinition Goal,
    ScheduledCommitmentState? Commitment = null);
