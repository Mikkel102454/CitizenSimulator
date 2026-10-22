using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.CitizenMind.Executor;
using CitizenSimulator.CitizenMind.Planning.Abstractions;
using CitizenSimulator.CitizenMind.Services;

namespace CitizenSimulator.CitizenMind.Planning.Scheduling;

internal enum PlanningJobKind { SEARCH, VALIDATION }

internal sealed class PlanningJob
{
    public required EntityRef Citizen { get; init; }
    public required PlanningJobKind Kind { get; init; }
    public required GoalSelection Selection { get; init; }
    public required long ScheduleRevision { get; init; }
    public required (int Urgency, long Importance, long Deadline) Priority { get; init; }
    public PlanExecutionState? ExpectedExecution { get; init; }
    public int ExpectedStepIndex { get; init; }
    public required Func<TimeSpan, IPlanningSearch> CreateSearch { get; init; }
    public required Func<PlanningJob, bool> IsRelevant { get; init; }
    public required Func<PlanningJob, bool> CanApply { get; init; }
    public required Func<PlanResult, TimeSpan, PlanResult?> Validate { get; init; }

    public long Order { get; set; }
    public IPlanningSearch? Search { get; private set; }
    public PlanResult? Candidate { get; private set; }
    public PlanResult? Result { get; private set; }
    public bool IsCompleted { get; private set; }
    public bool IsStale { get; private set; }

    public bool Matches(PlanningJob other)
        => ScheduleRevision == other.ScheduleRevision &&
           Selection.IsEmergency == other.Selection.IsEmergency && Priority == other.Priority &&
           ReferenceEquals(Selection.Goal, other.Selection.Goal) &&
           Selection.Commitment?.Commitment.Id == other.Selection.Commitment?.Commitment.Id &&
           ReferenceEquals(ExpectedExecution, other.ExpectedExecution) && ExpectedStepIndex == other.ExpectedStepIndex;

    public void AdvanceSearch(PlanningBudget budget, TimeSpan gameTime)
    {
        if (Search == null)
        {
            if (!budget.TryConsume()) return;
            Search = CreateSearch(gameTime);
        }
        Search.Advance(budget);
        Candidate = Search.Result;
        if (Candidate is { Status: not (PlanStatus.FOUND or PlanStatus.ALREADY_SATISFIED) })
        {
            Result = Candidate;
            IsCompleted = true;
        }
    }

    public void ValidateCandidate(PlanningBudget budget, TimeSpan gameTime)
    {
        if (!budget.TryConsume(PlanValidator.WorkCost(Candidate!))) return;
        Result = Validate(Candidate!, gameTime);
        IsStale = Result == null;
        IsCompleted = true;
    }

    public void FailBudget()
    {
        Result = new PlanResult { Status = PlanStatus.BUDGET_EXCEEDED };
        IsCompleted = true;
    }
}
