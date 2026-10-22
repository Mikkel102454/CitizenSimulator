using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Calender.Services.Abstractions;
using CitizenSimulator.CitizenMind.Components;
using CitizenSimulator.CitizenMind.Configuration;
using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Executor;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Planning;
using CitizenSimulator.CitizenMind.Planning.Abstractions;
using CitizenSimulator.CitizenMind.Planning.Scheduling;
using CitizenSimulator.CitizenMind.Services;
using CitizenSimulator.CitizenMind.Scheduling;

namespace CitizenSimulator.CitizenMind.Systems;

public class CitizenMindSystem(IEventBus events, GoalSelector goalSelector, CitizenMindService service,
    ICalenderService calender, ActionExecutor executor, PlanningSchedulerOptions? schedulerOptions = null,
    PlannerOptions? plannerOptions = null) : GameSystem
{
    private readonly PlannerOptions _plannerOptions = plannerOptions ?? new();
    private readonly PlanningWorkScheduler _planning = new(schedulerOptions ?? new());

    public int PendingPlanningCount => _planning.Count;
    public int PlanningWorkLastTick => _planning.WorkLastTick;
    public PlanningDiagnostics GetPlanningDiagnostics() => _planning.GetDiagnostics();

    protected override void OnStart(ICommandBuffer commands)
    {
        MindDefinitionValidator.Validate(service);
        service.CloseRegistration();
    }

    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {
        ulong ticksPerDay = calender.GetTicksPerDay();
        TimeSpan gameDelta = calender.GetGameTime(1);
        TimeSpan gameTime = calender.GetGameTime(time.Tick);
        ulong retryDelayTicks = Math.Max(1UL, (ulong)Math.Ceiling(ticksPerDay * (5.0 / 1440.0)));
        var citizens = Store.Query(MindComponent.GetComponentType()).ToArray();

        // Gather requests before spending any work, so an emergency encountered
        // later in the entity order can still take priority.
        foreach (var entity in citizens)
        {
            if (!Store.IsAlive(entity)) continue;
            var mind = Store.GetComponent<MindComponent>(entity);
            if (mind == null) continue;
            if (!mind.IsActive) { CleanupMind(entity, mind); continue; }

            DailyScheduleGenerator.Refresh(mind, gameTime, _plannerOptions.MaxPlanDuration, calender);
            RefreshSchedule(entity, mind, gameTime);
            if (mind.Execution is { Status: not PlanExecutionStatus.RUNNING })
                FinishExecution(entity, mind, time.Tick, retryDelayTicks, gameTime);

            if (mind.Execution != null && mind.CurrentCommitment is { } current &&
                (current.Status != CommitmentStatus.PENDING ||
                 !mind.Schedule.Any(entry => SameOccurrence(entry.Commitment, current.Commitment))) &&
                executor.Cancel(entity, mind.Execution))
            {
                ClearPlan(mind);
                _planning.Cancel(entity, PlanningJobKind.VALIDATION);
            }

            var wanted = goalSelector.SelectGoal(mind, time.Tick, gameTime);
            var replacement = ChooseReplacement(entity, mind, wanted, time.Tick, gameTime);
            if (replacement != null)
                QueueSearch(entity, mind, replacement);
            else
                _planning.Cancel(entity, PlanningJobKind.SEARCH);

            if (mind.Execution is { Started: false } execution &&
                mind.PlanScheduleRevision == mind.ScheduleRevision &&
                mind.CurrentGoal != null &&
                mind.Schedule.Any(entry => entry.Status == CommitmentStatus.PENDING) &&
                !IsCurrentEmergency(entity, mind, gameTime))
            {
                QueueValidation(entity, mind, execution);
            }
            else
            {
                _planning.Cancel(entity, PlanningJobKind.VALIDATION);
            }
        }

        _planning.Process(gameTime,
            job => CompletePlanning(job, time.Tick, retryDelayTicks, gameTime));

        // Executing an existing plan does not wait for an unrelated replacement
        // search. At action boundaries, schedule-sensitive plans require a fresh
        // validation ticket; running non-interruptible actions can finish.
        foreach (var entity in citizens)
        {
            if (!Store.IsAlive(entity)) continue;
            var mind = Store.GetComponent<MindComponent>(entity);
            if (mind is not { IsActive: true, Execution: { Status: PlanExecutionStatus.RUNNING } execution })
                continue;

            if (!execution.Started && !IsCurrentEmergency(entity, mind, gameTime) &&
                (mind.PlanScheduleRevision != mind.ScheduleRevision ||
                 (mind.Schedule.Any(entry => entry.Status == CommitmentStatus.PENDING) &&
                  mind.ValidatedAtTick != time.Tick)))
                continue;

            executor.Update(entity, execution, gameDelta, gameTime);
            if (execution.Status != PlanExecutionStatus.RUNNING)
                FinishExecution(entity, mind, time.Tick, retryDelayTicks, gameTime);
        }
    }

    private GoalSelection? ChooseReplacement(EntityRef citizen, MindComponent mind,
        GoalSelection? wanted, ulong currentTick, TimeSpan gameTime)
    {
        if (mind.Execution == null || mind.CurrentGoal == null)
        {
            // Utility score changes must not restart an otherwise relevant search
            // every tick. Emergencies and due commitments can still supersede it.
            var pending = _planning.Get(citizen, PlanningJobKind.SEARCH);
            if (wanted is { IsEmergency: false, Commitment: null } &&
                pending is { Selection: { IsEmergency: false, Commitment: null } } &&
                pending.ScheduleRevision == mind.ScheduleRevision &&
                (!mind.GoalRetryTicks.TryGetValue(pending.Selection.Goal.Id, out var retryTick) ||
                 currentTick >= retryTick))
            {
                var context = service.CreateEvaluationContext(citizen, gameTime);
                if (!ActionPlanner.IsGoalSatisfied(pending.Selection.Goal, context) &&
                    ExpressionEvaluator.Evaluate(context, pending.Selection.Goal.Eligible))
                    return pending.Selection;
            }
            return wanted;
        }
        bool currentEmergency = IsCurrentEmergency(citizen, mind, gameTime);

        if (wanted is { IsEmergency: true } &&
            (!currentEmergency || mind.CurrentCommitment != null ||
             wanted.Goal.Emergency!.Priority > mind.CurrentGoal.Emergency!.Priority))
            return wanted;

        if (!currentEmergency && wanted?.Commitment is { } incoming)
        {
            var current = mind.CurrentCommitment;
            if (current == null || current.Status != CommitmentStatus.PENDING ||
                incoming.Commitment.Priority > current.Commitment.Priority ||
                (incoming.Commitment.Priority == current.Commitment.Priority &&
                 incoming.Commitment.Window.End < current.Commitment.Window.End))
                return wanted;
        }

        if (!currentEmergency && mind.PlanScheduleRevision != mind.ScheduleRevision)
        {
            if (mind.CurrentCommitment is { } current)
            {
                var entry = mind.Schedule.FirstOrDefault(item =>
                    item.Status == CommitmentStatus.PENDING &&
                    SameOccurrence(item.Commitment, current.Commitment));
                return entry == null ? wanted : new GoalSelection(mind.CurrentGoal, entry);
            }
            return new GoalSelection(mind.CurrentGoal);
        }
        return null;
    }

    private void QueueSearch(EntityRef citizen, MindComponent mind, GoalSelection selection)
        => _planning.Enqueue(CreateJob(citizen, mind, selection, PlanningJobKind.SEARCH,
            (time, measurement) => CreateSearch(citizen, selection, mind, time, measurement)));

    private void QueueValidation(EntityRef citizen, MindComponent mind, PlanExecutionState execution)
    {
        var selection = new GoalSelection(mind.CurrentGoal!, mind.CurrentCommitment);
        var remaining = new ActionPlan
        {
            PlanSteps = execution.Plan.PlanSteps.Skip(execution.StepIndex).ToArray(),
            TotalCost = execution.Plan.TotalCost
        };
        var result = new PlanResult
        {
            Status = PlanStatus.FOUND, Plan = remaining, Reservations = execution.Reservations
        };
        _planning.Enqueue(CreateJob(citizen, mind, selection, PlanningJobKind.VALIDATION,
            (_, _) => new CompletedPlanningSearch(result), execution));
    }

    private PlanningJob CreateJob(EntityRef citizen, MindComponent mind, GoalSelection selection,
        PlanningJobKind kind, Func<TimeSpan, PlanningMeasurement, IPlanningSearch> create, PlanExecutionState? execution = null)
    {
        var measurement = new PlanningMeasurement();
        var priority = selection.IsEmergency
            ? (0, -(long)selection.Goal.Emergency!.Priority, long.MinValue)
            : selection.Commitment is { } entry
                ? (1, -(long)entry.Commitment.Priority, entry.Commitment.Window.End.Ticks)
                : (2, 0L, long.MaxValue);

        bool IsRelevant(PlanningJob job)
            => Store.IsAlive(citizen) && mind.IsActive &&
               ReferenceEquals(Store.GetComponent<MindComponent>(citizen), mind) &&
               job.ScheduleRevision == mind.ScheduleRevision &&
               (selection.Commitment == null ||
                mind.Schedule.Any(item => item.Status == CommitmentStatus.PENDING &&
                    SameOccurrence(item.Commitment, selection.Commitment.Commitment))) &&
               (kind != PlanningJobKind.VALIDATION ||
                (ReferenceEquals(mind.Execution, execution) &&
                 execution!.StepIndex == job.ExpectedStepIndex && !execution.Started));

        bool CanApply(PlanningJob job)
            => kind == PlanningJobKind.VALIDATION || mind.Execution is not { Started: true } active ||
               active.Plan.PlanSteps[active.StepIndex].Candidate.Definition.Execution.Interruptible;

        return new PlanningJob
        {
            Citizen = citizen, Kind = kind, Selection = selection,
            ScheduleRevision = mind.ScheduleRevision, Priority = priority,
            ExpectedExecution = execution, ExpectedStepIndex = execution?.StepIndex ?? 0,
            Measurement = measurement,
            CreateSearch = time => create(time, measurement), IsRelevant = IsRelevant, CanApply = CanApply,
            Validate = (result, time) => ValidateResult(citizen, selection, mind, result, time, measurement)
        };
    }

    private IPlanningSearch CreateSearch(EntityRef citizen, GoalSelection selection,
        MindComponent mind, TimeSpan gameTime, PlanningMeasurement measurement)
    {
        var state = service.CreateEvaluationContext(citizen, gameTime);
        var horizon = gameTime + _plannerOptions.MaxPlanDuration;
        var remaining = GetRemainingCommitments(mind, selection, horizon);
        var goals = remaining.Select(entry => service.Goals[entry.GoalId]).Prepend(selection.Goal);
        var providers = CreateProviders(citizen, goals, measurement);
        int checks = 0;

        IPlanningSearch Forecast(int index, EvaluationContext projected)
        {
            if (index >= remaining.Length)
                return new CompletedPlanningSearch(new PlanResult { Status = PlanStatus.FOUND });
            if (checks++ >= _plannerOptions.MaxScheduleChecks)
                return new CompletedPlanningSearch(new PlanResult { Status = PlanStatus.BUDGET_EXCEEDED });

            var commitment = remaining[index];
            var goal = service.Goals[commitment.GoalId];
            if (projected.GameTime > commitment.Window.End)
                return new CompletedPlanningSearch(new PlanResult { Status = PlanStatus.NO_PATH });

            if (projected.GameTime >= commitment.Window.Start && ActionPlanner.IsGoalSatisfied(goal, projected))
            {
                return new ScheduleForecastSearch(Forecast(index + 1, projected), commitment, goal,
                    new ActionPlan { PlanSteps = [] });
            }

            var available = horizon - projected.GameTime;
            if (available <= TimeSpan.Zero)
                return new CompletedPlanningSearch(new PlanResult { Status = PlanStatus.BUDGET_EXCEEDED });
            var options = new PlannerOptions
            {
                MaxDepth = _plannerOptions.MaxDepth,
                MaxExpansions = _plannerOptions.MaxExpansions,
                MaxCandidatesPerAction = _plannerOptions.MaxCandidatesPerAction,
                MaxPlanDuration = available,
                MaxScheduleChecks = _plannerOptions.MaxScheduleChecks
            };
            var search = ActionPlanner.CreateSearch(goal, projected, service.Actions, service.FactDescriptors,
                providers, options, service.TimePredictors, new PlanConstraints
                {
                    GoalWindow = commitment.Window,
                    CreateContinuation = next => Forecast(index + 1, next)
                });
            return new ScheduleForecastSearch(search, commitment, goal);
        }

        return ActionPlanner.CreateSearch(selection.Goal, state, service.Actions, service.FactDescriptors,
            providers, _plannerOptions, service.TimePredictors, new PlanConstraints
            {
                GoalWindow = selection.Commitment?.Commitment.Window,
                CreateContinuation = remaining.Length == 0 ? null : projected => Forecast(0, projected)
            });
    }

    private ScheduledCommitment[] GetRemainingCommitments(MindComponent mind,
        GoalSelection selection, TimeSpan horizon)
        => mind.Schedule.Where(entry => !selection.IsEmergency &&
                entry.Status == CommitmentStatus.PENDING &&
                entry.Commitment.Id != selection.Commitment?.Commitment.Id &&
                entry.Commitment.Window.Start <= horizon &&
                (selection.Commitment == null ||
                 entry.Commitment.Priority >= selection.Commitment.Commitment.Priority))
            .OrderBy(entry => entry.Commitment.Window.End)
            .ThenByDescending(entry => entry.Commitment.Priority)
            .ThenBy(entry => entry.Commitment.Id)
            .Select(entry => entry.Commitment).ToArray();

    private IReadOnlyDictionary<string, CandidateProviderSnapshot> CreateProviders(
        EntityRef citizen, IEnumerable<GoalDefinition> goals, PlanningMeasurement measurement)
    {
        var actions = goals.SelectMany(goal => ActionDependencyResolver.Resolve(goal, service.Actions))
            .DistinctBy(action => action.Id).ToArray();
        return service.CreateCandidateProviders(citizen, actions, measurement);
    }

    private PlanResult? ValidateResult(EntityRef citizen, GoalSelection selection,
        MindComponent mind, PlanResult result, TimeSpan gameTime, PlanningMeasurement measurement)
    {
        var state = service.CreateEvaluationContext(citizen, gameTime);
        if (selection.IsEmergency && (selection.Goal.Emergency == null ||
            !ExpressionEvaluator.Evaluate(state, selection.Goal.Emergency.When)))
            return null;
        var remaining = GetRemainingCommitments(mind, selection, gameTime + _plannerOptions.MaxPlanDuration);
        var providers = CreateProviders(citizen,
            remaining.Select(entry => service.Goals[entry.GoalId]).Prepend(selection.Goal), measurement);
        return PlanValidator.Validate(selection.Goal, result, state, service.FactDescriptors,
            providers, service.TimePredictors, _plannerOptions, selection.Commitment?.Commitment.Window, remaining);
    }

    private void CompletePlanning(PlanningJob job, ulong tick, ulong retryDelayTicks, TimeSpan gameTime)
    {
        if (!job.IsRelevant(job)) return;
        var mind = Store.GetComponent<MindComponent>(job.Citizen)!;
        if (job.IsStale)
        {
            if (job.Kind == PlanningJobKind.VALIDATION && mind.Execution != null &&
                executor.Cancel(job.Citizen, mind.Execution))
                ClearPlan(mind);
            return;
        }

        var result = job.Result!;
        if (result.Status is PlanStatus.NO_PATH or PlanStatus.BUDGET_EXCEEDED)
        {
            mind.GoalRetryTicks[job.Selection.Goal.Id] = checked(tick + retryDelayTicks);
            if (job.Kind == PlanningJobKind.VALIDATION && mind.Execution != null &&
                executor.Cancel(job.Citizen, mind.Execution))
                ClearPlan(mind);
            return;
        }

        if (result.Status == PlanStatus.ALREADY_SATISFIED)
        {
            if (job.Selection.Commitment != null)
                job.Selection.Commitment.Status = CommitmentStatus.COMPLETED;
            if (mind.Execution != null && (job.Kind == PlanningJobKind.VALIDATION ||
                (mind.CurrentGoal == job.Selection.Goal &&
                 mind.CurrentCommitment?.Commitment.Id == job.Selection.Commitment?.Commitment.Id)) &&
                executor.Cancel(job.Citizen, mind.Execution))
                ClearPlan(mind);
            return;
        }

        if (mind.Execution != null && !executor.Cancel(job.Citizen, mind.Execution)) return;
        if (!job.IsRelevant(job)) return;
        StartPlan(mind, job.Selection, result, tick);
        _planning.Cancel(job.Citizen, PlanningJobKind.VALIDATION);
    }

    private void RefreshSchedule(EntityRef citizen, MindComponent mind, TimeSpan gameTime)
    {
        if (!mind.Schedule.Any(entry => entry.Status == CommitmentStatus.PENDING)) return;
        var context = service.CreateEvaluationContext(citizen, gameTime);
        foreach (var entry in mind.Schedule.Where(entry => entry.Status == CommitmentStatus.PENDING))
        {
            if (gameTime > entry.Commitment.Window.End)
            {
                entry.Status = CommitmentStatus.MISSED;
                continue;
            }
            if (mind.Execution is { Status: PlanExecutionStatus.RUNNING } &&
                mind.CurrentCommitment?.Commitment.Id == entry.Commitment.Id)
                continue;
            if (gameTime >= entry.Commitment.Window.Start &&
                ActionPlanner.IsGoalSatisfied(service.Goals[entry.Commitment.GoalId], context))
                entry.Status = CommitmentStatus.COMPLETED;
        }
    }

    private bool IsCurrentEmergency(EntityRef citizen, MindComponent mind, TimeSpan gameTime)
        => mind.CurrentGoal?.Emergency is { } emergency &&
           ExpressionEvaluator.Evaluate(service.CreateEvaluationContext(citizen, gameTime), emergency.When);

    private static bool SameOccurrence(ScheduledCommitment first, ScheduledCommitment second)
        => first.Id == second.Id && first.GoalId == second.GoalId &&
           first.Window.Start == second.Window.Start && first.Window.End == second.Window.End;

    private static void StartPlan(MindComponent mind, GoalSelection selection, PlanResult result, ulong tick)
    {
        mind.CurrentGoal = selection.Goal;
        mind.CurrentCommitment = selection.Commitment;
        mind.PlanScheduleRevision = mind.ScheduleRevision;
        mind.ValidatedAtTick = tick;
        mind.Execution = new PlanExecutionState
        {
            Plan = result.Plan ?? throw new InvalidOperationException("A found result must contain a plan."),
            Reservations = result.Reservations
        };
    }

    private static void ClearPlan(MindComponent mind)
    {
        mind.Execution = null;
        mind.CurrentGoal = null;
        mind.CurrentCommitment = null;
        mind.ValidatedAtTick = null;
    }

    private void FinishExecution(EntityRef citizen, MindComponent mind, ulong currentTick,
        ulong retryDelayTicks, TimeSpan gameTime)
    {
        _planning.Cancel(citizen, PlanningJobKind.VALIDATION);
        if (mind.Execution?.Status == PlanExecutionStatus.FAILED && mind.CurrentGoal != null)
            mind.GoalRetryTicks[mind.CurrentGoal.Id] = checked(currentTick + retryDelayTicks);

        if (mind.Execution?.Status == PlanExecutionStatus.COMPLETED && mind.CurrentCommitment is { } current)
        {
            var entry = mind.Schedule.FirstOrDefault(entry => SameOccurrence(entry.Commitment, current.Commitment));
            if (entry is { Status: CommitmentStatus.PENDING })
            {
                var context = service.CreateEvaluationContext(citizen, gameTime);
                if (gameTime >= entry.Commitment.Window.Start && gameTime <= entry.Commitment.Window.End &&
                    ActionPlanner.IsGoalSatisfied(service.Goals[entry.Commitment.GoalId], context))
                    entry.Status = CommitmentStatus.COMPLETED;
                else if (gameTime > entry.Commitment.Window.End)
                    entry.Status = CommitmentStatus.MISSED;
            }
        }
        ClearPlan(mind);
    }

    protected override void OnStop(ICommandBuffer commands)
    {
        _planning.Clear();
        var errors = new List<Exception>();
        foreach (var citizen in Store.Query(MindComponent.GetComponentType()).ToArray())
        {
            try
            {
                var mind = Store.GetComponent<MindComponent>(citizen);
                if (mind != null) CleanupMind(citizen, mind);
            }
            catch (Exception exception)
            {
                errors.Add(new InvalidOperationException(
                    $"Failed to clean up citizen '{citizen}'.", exception));
            }
        }
        if (errors.Count > 0)
            throw new AggregateException("CitizenMind shutdown cleanup failed.", errors);
    }

    protected override void OnRemove(EntityRef entity, IComponent component)
    {
        if (component is MindComponent mind) CleanupMind(entity, mind);
    }

    private void CleanupMind(EntityRef citizen, MindComponent mind)
    {
        _planning.Cancel(citizen);
        try
        {
            if (mind.Execution?.Status == PlanExecutionStatus.RUNNING)
                executor.Cancel(citizen, mind.Execution, force: true);
        }
        finally
        {
            ClearPlan(mind);
            mind.Schedule.Clear();
            mind.DailyScheduleRules.Clear();
            mind.GoalRetryTicks.Clear();
            mind.EmergencyActive.Clear();
        }
    }
}
