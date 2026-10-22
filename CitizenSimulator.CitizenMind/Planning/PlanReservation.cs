using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Scheduling;

namespace CitizenSimulator.CitizenMind.Planning;

/// <summary>A forecast route witnessing that a future commitment can still be fulfilled.</summary>
public sealed record PlanReservation(ScheduledCommitment Commitment, GoalDefinition Goal, ActionPlan Plan);
