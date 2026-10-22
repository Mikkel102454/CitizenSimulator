using System.Diagnostics;
using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.CitizenMind.Planning;

public enum PlanningRequestOutcome { SUCCEEDED, NO_PATH, BUDGET_EXCEEDED, STALE, CANCELLED }

/// <summary>Wall-clock durations exclude paused time from ActiveTime. Forecast expansions are included.</summary>
public sealed record PlanningRequestDiagnostics(EntityRef Citizen, string GoalId, bool IsValidation,
    PlanningRequestOutcome Outcome, TimeSpan QueueWait, TimeSpan Lifetime, TimeSpan ActiveTime,
    long Expansions, long WorkUnits);

public sealed record CandidateProviderDiagnostics(string ProviderId, long SnapshotCalls,
    TimeSpan SnapshotTime, long CandidateCalls, TimeSpan CandidateTime);

/// <summary>A detached snapshot. Totals cover the system lifetime; recent requests are bounded to 64.</summary>
public sealed record PlanningDiagnostics(int PendingRequests, int WorkLastTick, long Succeeded,
    long NoPath, long BudgetExceeded, long Stale, long Cancelled,
    IReadOnlyList<PlanningRequestDiagnostics> RecentRequests,
    IReadOnlyList<CandidateProviderDiagnostics> Providers);

internal sealed class PlanningMeasurement
{
    public long EnqueuedAt { get; } = Stopwatch.GetTimestamp();
    public long? StartedAt { get; set; }
    public TimeSpan ActiveTime { get; set; }
    public long Expansions { get; set; }
    public long WorkUnits { get; set; }
    public Dictionary<string, CandidateProviderDiagnostics> Providers { get; } = [];

    public void RecordProvider(string id, TimeSpan elapsed, bool snapshot)
    {
        var old = Providers.GetValueOrDefault(id) ?? new(id, 0, TimeSpan.Zero, 0, TimeSpan.Zero);
        Providers[id] = snapshot
            ? old with { SnapshotCalls = old.SnapshotCalls + 1, SnapshotTime = old.SnapshotTime + elapsed }
            : old with { CandidateCalls = old.CandidateCalls + 1, CandidateTime = old.CandidateTime + elapsed };
    }
}
