using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.CitizenMind.Planning.Scheduling;

internal sealed class PlanningWorkScheduler
{
    private readonly PlanningSchedulerOptions _options;
    private readonly Dictionary<(EntityRef Citizen, PlanningJobKind Kind), PlanningJob> _jobs = [];
    private long _order;
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
        job.Order = _order++;
        _jobs[key] = job;
    }

    public void Cancel(EntityRef citizen, PlanningJobKind kind) => _jobs.Remove((citizen, kind));
    public PlanningJob? Get(EntityRef citizen, PlanningJobKind kind)
        => _jobs.GetValueOrDefault((citizen, kind));
    public void Cancel(EntityRef citizen)
    {
        Cancel(citizen, PlanningJobKind.SEARCH);
        Cancel(citizen, PlanningJobKind.VALIDATION);
    }
    public void Clear() { _jobs.Clear(); WorkLastTick = 0; }

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
            if (!job.IsRelevant(job)) { _jobs.Remove(key); continue; }

            if (job.Candidate != null)
            {
                if (!job.CanApply(job)) continue;
                int cost = PlanValidator.WorkCost(job.Candidate);
                if (cost > _options.MaxExpansionsPerTick) job.FailBudget();
                else if (cost > budget.Remaining) continue;
                else job.ValidateCandidate(budget, gameTime);
            }
            else
            {
                var slice = new PlanningBudget(Math.Min(_options.ExpansionsPerSlice, budget.Remaining));
                job.AdvanceSearch(slice, gameTime);
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
            completed(job);
        }

        WorkLastTick = budget.Consumed;
    }
}
