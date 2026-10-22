using CitizenSimulator.Abstractions.ECS;
using System.Diagnostics;

namespace CitizenSimulator.CitizenMind.Planning.Scheduling;

internal sealed class PlanningWorkScheduler
{
    private readonly PlanningSchedulerOptions _options;
    private readonly Dictionary<(EntityRef Citizen, PlanningJobKind Kind), PlanningJob> _jobs = [];
    private long _order;
    private readonly Queue<PlanningRequestDiagnostics> _recent = new();
    private readonly long[] _outcomes = new long[Enum.GetValues<PlanningRequestOutcome>().Length];
    private readonly Dictionary<string, CandidateProviderDiagnostics> _providers = [];
    public int Count => _jobs.Count;
    public int WorkLastTick { get; private set; }

    public PlanningWorkScheduler(PlanningSchedulerOptions options)
    {
        if (options.MaxExpansionsPerTick < 1) throw new ArgumentOutOfRangeException(nameof(options.MaxExpansionsPerTick));
        if (options.ExpansionsPerSlice < 1) throw new ArgumentOutOfRangeException(nameof(options.ExpansionsPerSlice));
        _options = options;
    }

    public void Enqueue(PlanningJob job)
    {
        var key = (job.Citizen, job.Kind);
        if (_jobs.TryGetValue(key, out var existing) && existing.Matches(job)) return;
        if (existing != null) Record(existing, PlanningRequestOutcome.CANCELLED);
        job.Order = _order++;
        _jobs[key] = job;
    }

    public void Cancel(EntityRef citizen, PlanningJobKind kind)
    {
        if (_jobs.Remove((citizen, kind), out var job)) Record(job, PlanningRequestOutcome.CANCELLED);
    }
    public PlanningJob? Get(EntityRef citizen, PlanningJobKind kind)
        => _jobs.GetValueOrDefault((citizen, kind));
    public void Cancel(EntityRef citizen)
    {
        Cancel(citizen, PlanningJobKind.SEARCH);
        Cancel(citizen, PlanningJobKind.VALIDATION);
    }
    public void Clear()
    {
        foreach (var job in _jobs.Values) Record(job, PlanningRequestOutcome.CANCELLED);
        _jobs.Clear();
        WorkLastTick = 0;
    }

    public PlanningDiagnostics GetDiagnostics()
    {
        var providers = new Dictionary<string, CandidateProviderDiagnostics>(_providers);
        foreach (var job in _jobs.Values)
            MergeProviders(providers, job.Measurement.Providers.Values);
        return new(Count, WorkLastTick,
            _outcomes[(int)PlanningRequestOutcome.SUCCEEDED], _outcomes[(int)PlanningRequestOutcome.NO_PATH],
            _outcomes[(int)PlanningRequestOutcome.BUDGET_EXCEEDED], _outcomes[(int)PlanningRequestOutcome.STALE],
            _outcomes[(int)PlanningRequestOutcome.CANCELLED],
            Array.AsReadOnly(_recent.ToArray()),
            Array.AsReadOnly(providers.Values.OrderBy(provider => provider.ProviderId).ToArray()));
    }

    private static void MergeProviders(Dictionary<string, CandidateProviderDiagnostics> target,
        IEnumerable<CandidateProviderDiagnostics> providers)
    {
        foreach (var provider in providers)
        {
            var old = target.GetValueOrDefault(provider.ProviderId)
                ?? new(provider.ProviderId, 0, TimeSpan.Zero, 0, TimeSpan.Zero);
            target[provider.ProviderId] = new(provider.ProviderId,
                old.SnapshotCalls + provider.SnapshotCalls, old.SnapshotTime + provider.SnapshotTime,
                old.CandidateCalls + provider.CandidateCalls, old.CandidateTime + provider.CandidateTime);
        }
    }

    private void Record(PlanningJob job, PlanningRequestOutcome outcome)
    {
        var measurement = job.Measurement;
        long now = Stopwatch.GetTimestamp();
        _outcomes[(int)outcome]++;
        _recent.Enqueue(new(job.Citizen, job.Selection.Goal.Id, job.Kind == PlanningJobKind.VALIDATION,
            outcome, Stopwatch.GetElapsedTime(measurement.EnqueuedAt, measurement.StartedAt ?? now),
            Stopwatch.GetElapsedTime(measurement.EnqueuedAt, now), measurement.ActiveTime,
            measurement.Expansions, measurement.WorkUnits));
        if (_recent.Count > 64) _recent.Dequeue();
        MergeProviders(_providers, measurement.Providers.Values);
    }

    public void Process(TimeSpan gameTime, Action<PlanningJob> completed)
    {
        var budget = new PlanningBudget(_options.MaxExpansionsPerTick);
        var queue = new PriorityQueue<PlanningJob, (int Urgency, long Importance, long Deadline, long Order)>();
        void Push(PlanningJob job)
            => queue.Enqueue(job, (job.Priority.Urgency, job.Priority.Importance, job.Priority.Deadline, job.Order));
        foreach (var job in _jobs.Values) Push(job);

        while (budget.Remaining > 0 && queue.TryDequeue(out var job, out _))
        {
            var key = (job.Citizen, job.Kind);
            if (!_jobs.TryGetValue(key, out var active) || !ReferenceEquals(job, active)) continue;
            if (!job.IsRelevant(job))
            {
                _jobs.Remove(key);
                Record(job, PlanningRequestOutcome.STALE);
                continue;
            }

            if (job.Candidate != null)
            {
                if (!job.CanApply(job)) continue;
                int cost = PlanValidator.WorkCost(job.Candidate);
                if (cost > _options.MaxExpansionsPerTick) job.FailBudget();
                else if (cost > budget.Remaining) continue;
                else
                {
                    long started = Stopwatch.GetTimestamp();
                    job.Measurement.StartedAt ??= started;
                    int before = budget.Consumed;
                    try { job.ValidateCandidate(budget, gameTime); }
                    finally
                    {
                        job.Measurement.ActiveTime += Stopwatch.GetElapsedTime(started);
                        job.Measurement.WorkUnits += budget.Consumed - before;
                    }
                }
            }
            else
            {
                var slice = new PlanningBudget(Math.Min(_options.ExpansionsPerSlice, budget.Remaining))
                {
                    Measurement = job.Measurement
                };
                long started = Stopwatch.GetTimestamp();
                job.Measurement.StartedAt ??= started;
                try { job.AdvanceSearch(slice, gameTime); }
                finally
                {
                    job.Measurement.ActiveTime += Stopwatch.GetElapsedTime(started);
                    job.Measurement.WorkUnits += slice.Consumed;
                }
                if (slice.Consumed > 0) budget.TryConsume(slice.Consumed);
                if (!job.IsCompleted)
                {
                    if (slice.Consumed == 0 && job.Candidate == null) continue;
                    job.Order = _order++;
                    Push(job);
                    continue;
                }
            }

            _jobs.Remove(key);
            Record(job, job.IsStale ? PlanningRequestOutcome.STALE : job.Result!.Status switch
            {
                PlanStatus.FOUND or PlanStatus.ALREADY_SATISFIED => PlanningRequestOutcome.SUCCEEDED,
                PlanStatus.NO_PATH => PlanningRequestOutcome.NO_PATH,
                _ => PlanningRequestOutcome.BUDGET_EXCEEDED
            });
            completed(job);
        }

        WorkLastTick = budget.Consumed;
    }
}
