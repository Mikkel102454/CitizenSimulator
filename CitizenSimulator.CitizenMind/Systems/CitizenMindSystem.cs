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
using CitizenSimulator.CitizenMind.Services;
using CitizenSimulator.CitizenMind.Scheduling;

namespace CitizenSimulator.CitizenMind.Systems;

public class CitizenMindSystem(IEventBus events, GoalSelector goalSelector, CitizenMindService service, ICalenderService calender, ActionExecutor executor) : GameSystem
{
    private readonly PlannerOptions _plannerOptions = new();
    
    protected override void OnStart(ICommandBuffer commands)
    {
        MindDefinitionValidator.Validate(service);
        service.CloseRegistration();
    }
    
    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {
        ulong ticksPerDay = calender.GetTicksPerDay();

        TimeSpan gameDelta = TimeSpan.FromMinutes(1440.0 / ticksPerDay);
        TimeSpan gameTime = calender.GetGameTime(time.Tick);

        ulong retryDelayTicks = Math.Max(1UL, (ulong)Math.Ceiling(ticksPerDay * (5.0 / 1440.0)));
        
        foreach (var entity in Store.Query(MindComponent.GetComponentType()))
        {
            var mind = Store.GetComponent<MindComponent>(entity);
            if(mind == null)
                continue;
            
            if (!mind.IsActive)
            {
                CleanupMind(entity, mind);
                continue;
            }

            RefreshSchedule(entity, mind, gameTime);

            if (mind.Execution is { Status: not PlanExecutionStatus.RUNNING })
                FinishExecution(entity, mind, time.Tick, retryDelayTicks, gameTime);

            if (mind.Execution != null &&
                (mind.PlanScheduleRevision != mind.ScheduleRevision ||
                 mind.CurrentCommitment is { Status: not CommitmentStatus.PENDING }) &&
                !IsCurrentEmergency(entity, mind, gameTime) &&
                executor.Cancel(entity, mind.Execution))
            {
                ClearPlan(mind);
            }
            
            if (mind.Execution != null)
            {
                if (TrySwitchToEmergency(entity, mind, time.Tick, retryDelayTicks) ||
                    TrySwitchToCommitment(entity, mind, time.Tick, retryDelayTicks, gameTime))
                    continue;

                if (!mind.Execution.Started &&
                    mind.Schedule.Any(entry => entry.Status == CommitmentStatus.PENDING) &&
                    !IsCurrentEmergency(entity, mind, gameTime) &&
                    !CanContinuePlan(entity, mind, gameTime))
                {
                    executor.Cancel(entity, mind.Execution);
                    ClearPlan(mind);
                }
                else
                {
                    executor.Update(entity, mind.Execution, gameDelta, gameTime);
                    if (mind.Execution.Status != PlanExecutionStatus.RUNNING)
                        FinishExecution(entity, mind, time.Tick, retryDelayTicks, gameTime);
                    continue;
                }
            }
            
            var selection = goalSelector.SelectGoal(mind, time.Tick, gameTime);

            if (selection == null)
            {
                mind.CurrentGoal = null;
                continue;
            }

            var goal = selection.Goal;
            var result = PlanForGoal(entity, selection, mind, gameTime);

            switch (result.Status)
            {
                case PlanStatus.FOUND:
                    StartPlan(mind, selection, result);
                    break;

                case PlanStatus.ALREADY_SATISFIED:
                    if (selection.Commitment != null)
                        selection.Commitment.Status = CommitmentStatus.COMPLETED;
                    mind.CurrentGoal = null;
                    break;

                case PlanStatus.NO_PATH:
                case PlanStatus.BUDGET_EXCEEDED:
                    mind.GoalRetryTicks[goal.Id] =
                        checked(time.Tick + retryDelayTicks);

                    mind.CurrentGoal = null;
                    break;

                default:
                    throw new InvalidOperationException($"Unknown plan status '{result.Status}'.");
            }
        }
    }
    
    protected override void OnStop(ICommandBuffer commands)
    {
        var errors = new List<Exception>();

        var citizens = Store.Query(MindComponent.GetComponentType()).ToArray();

        foreach (var citizen in citizens)
        {
            try
            {
                var mind = Store.GetComponent<MindComponent>(citizen);

                if (mind != null)
                    CleanupMind(citizen, mind);
            }
            catch (Exception exception)
            {
                errors.Add(new InvalidOperationException(
                    $"Failed to clean up citizen '{citizen}'.",
                    exception));
            }
        }

        if (errors.Count > 0)
            throw new AggregateException(
                "CitizenMind shutdown cleanup failed.",
                errors);
    }

    protected override void OnRemove(EntityRef entity, IComponent component)
    {
        if (component is MindComponent mind)
            CleanupMind(entity, mind);
    }

    private PlanResult PlanForGoal(EntityRef citizen, GoalSelection selection, MindComponent mind, TimeSpan gameTime)
    {
        var request = CreatePlanningRequest(citizen, selection, mind, gameTime);

        return ActionPlanner.FindPlan(selection.Goal, request.State, service.Actions, service.FactDescriptors,
            request.Providers, _plannerOptions, service.TimePredictors, request.Constraints);
    }
    
    private bool TrySwitchToEmergency(EntityRef citizen, MindComponent mind, ulong currentTick, ulong retryDelayTicks)
    {
        var gameTime = calender.GetGameTime(currentTick);
        
        var emergency = goalSelector.SelectGoals(mind, currentTick, gameTime, emergenciesOnly: true);

        if (emergency == null ||
            (emergency.Id == mind.CurrentGoal?.Id && mind.CurrentCommitment == null))
            return false;

        var execution = mind.Execution;

        if (execution == null || execution.Status != PlanExecutionStatus.RUNNING)
            return false;

        // an equally or more urgent running goal keeps its plan.
        var currentEmergency = mind.CurrentGoal?.Emergency;

        if (currentEmergency != null && mind.CurrentCommitment == null)
        {
            var live = service.CreateEvaluationContext(citizen, gameTime);

            if (ExpressionEvaluator.Evaluate(live, currentEmergency.When) && currentEmergency.Priority >= emergency.Emergency!.Priority)
                return false;
        }

        if (execution.Started)
        {
            var action = execution.Plan.PlanSteps[execution.StepIndex].Candidate.Definition;

            if (!action.Execution.Interruptible)
                return false;
        }

        var selection = new GoalSelection(emergency);
        var result = PlanForGoal(citizen, selection, mind, gameTime);

        if (result.Status == PlanStatus.NO_PATH || result.Status == PlanStatus.BUDGET_EXCEEDED)
        {
            mind.GoalRetryTicks[emergency.Id] = checked(currentTick + retryDelayTicks);

            return false;
        }

        if (result.Status != PlanStatus.FOUND)
            return false;

        if (!executor.Cancel(citizen, execution))
            return false;

        StartPlan(mind, selection, result);

        return true;
    }

    private bool TrySwitchToCommitment(EntityRef citizen, MindComponent mind,
        ulong currentTick, ulong retryDelayTicks, TimeSpan gameTime)
    {
        if (IsCurrentEmergency(citizen, mind, gameTime))
            return false;

        var selection = goalSelector.SelectGoal(mind, currentTick, gameTime);
        if (selection?.Commitment == null ||
            selection.Commitment.Commitment.Id == mind.CurrentCommitment?.Commitment.Id)
            return false;

        var current = mind.CurrentCommitment;
        if (current is { Status: CommitmentStatus.PENDING } &&
            (current.Commitment.Priority > selection.Commitment.Commitment.Priority ||
             (current.Commitment.Priority == selection.Commitment.Commitment.Priority &&
              current.Commitment.Window.End <= selection.Commitment.Commitment.Window.End)))
            return false;

        var execution = mind.Execution!;
        if (execution.Started &&
            !execution.Plan.PlanSteps[execution.StepIndex].Candidate.Definition.Execution.Interruptible)
            return false;

        var result = PlanForGoal(citizen, selection, mind, gameTime);
        if (result.Status == PlanStatus.ALREADY_SATISFIED)
        {
            selection.Commitment.Status = CommitmentStatus.COMPLETED;
            return false;
        }

        if (result.Status != PlanStatus.FOUND)
        {
            mind.GoalRetryTicks[selection.Goal.Id] = checked(currentTick + retryDelayTicks);
            return false;
        }

        if (!executor.Cancel(citizen, execution))
            return false;

        StartPlan(mind, selection, result);
        return true;
    }

    private void RefreshSchedule(EntityRef citizen, MindComponent mind, TimeSpan gameTime)
    {
        if (!mind.Schedule.Any(entry => entry.Status == CommitmentStatus.PENDING))
            return;

        var context = service.CreateEvaluationContext(citizen, gameTime);
        foreach (var entry in mind.Schedule.Where(entry => entry.Status == CommitmentStatus.PENDING))
        {
            if (gameTime > entry.Commitment.Window.End)
            {
                entry.Status = CommitmentStatus.MISSED;
                continue;
            }

            // Let an executing plan finish its handlers before recording its outcome.
            if (mind.Execution is { Status: PlanExecutionStatus.RUNNING } &&
                mind.CurrentCommitment?.Commitment.Id == entry.Commitment.Id)
                continue;

            if (gameTime >= entry.Commitment.Window.Start &&
                GoalSatisfied(service.Goals[entry.Commitment.GoalId], context))
                entry.Status = CommitmentStatus.COMPLETED;
        }
    }

    private bool IsCurrentEmergency(EntityRef citizen, MindComponent mind, TimeSpan gameTime)
        => mind.CurrentGoal?.Emergency is { } emergency &&
           ExpressionEvaluator.Evaluate(service.CreateEvaluationContext(citizen, gameTime), emergency.When);

    private static bool GoalSatisfied(GoalDefinition goal, EvaluationContext state)
        => goal.Desired.All(condition => ExpressionEvaluator.Evaluate(state, condition));

    private static void StartPlan(MindComponent mind, GoalSelection selection, PlanResult result)
    {
        mind.CurrentGoal = selection.Goal;
        mind.CurrentCommitment = selection.Commitment;
        mind.PlanScheduleRevision = mind.ScheduleRevision;
        mind.Execution = new PlanExecutionState
        {
            Plan = result.Plan ?? throw new InvalidOperationException("A found result must contain a plan.")
        };
    }

    private static void ClearPlan(MindComponent mind)
    {
        mind.Execution = null;
        mind.CurrentGoal = null;
        mind.CurrentCommitment = null;
    }

    private void FinishExecution(EntityRef citizen, MindComponent mind, ulong currentTick,
        ulong retryDelayTicks, TimeSpan gameTime)
    {
        if (mind.Execution?.Status == PlanExecutionStatus.FAILED && mind.CurrentGoal != null)
            mind.GoalRetryTicks[mind.CurrentGoal.Id] = checked(currentTick + retryDelayTicks);

        if (mind.Execution?.Status == PlanExecutionStatus.COMPLETED &&
            mind.CurrentCommitment is { Status: CommitmentStatus.PENDING } entry)
        {
            var context = service.CreateEvaluationContext(citizen, gameTime);
            if (gameTime >= entry.Commitment.Window.Start && gameTime <= entry.Commitment.Window.End &&
                GoalSatisfied(service.Goals[entry.Commitment.GoalId], context))
                entry.Status = CommitmentStatus.COMPLETED;
            else if (gameTime > entry.Commitment.Window.End)
                entry.Status = CommitmentStatus.MISSED;
        }

        ClearPlan(mind);
    }

    private PlanningRequest CreatePlanningRequest(EntityRef citizen, GoalSelection selection,
        MindComponent mind, TimeSpan gameTime)
    {
        var state = service.CreateEvaluationContext(citizen, gameTime);
        var horizon = gameTime + _plannerOptions.MaxPlanDuration;
        bool emergency = selection.Commitment == null && selection.Goal.Emergency is { } emergencyDefinition &&
                         ExpressionEvaluator.Evaluate(state, emergencyDefinition.When);

        var remaining = mind.Schedule
            .Where(entry => !emergency && entry.Status == CommitmentStatus.PENDING &&
                entry.Commitment.Id != selection.Commitment?.Commitment.Id &&
                entry.Commitment.Window.Start <= horizon &&
                (selection.Commitment == null ||
                 entry.Commitment.Priority >= selection.Commitment.Commitment.Priority))
            .OrderBy(entry => entry.Commitment.Window.End)
            .ThenByDescending(entry => entry.Commitment.Priority)
            .ThenBy(entry => entry.Commitment.Id)
            .ToArray();

        var goals = remaining.Select(entry => service.Goals[entry.Commitment.GoalId])
            .Prepend(selection.Goal);
        var relevantActions = goals.SelectMany(goal => ActionDependencyResolver.Resolve(goal, service.Actions))
            .DistinctBy(action => action.Id).ToArray();
        var providers = service.CreateCandidateProviders(citizen, relevantActions);
        int checks = 0;

        PlanStatus CheckContinuation(EvaluationContext projected) => CheckRemaining(0, projected);

        PlanStatus CheckRemaining(int index, EvaluationContext projected)
        {
            if (index >= remaining.Length)
                return PlanStatus.FOUND;

            var commitment = remaining[index].Commitment;
            var goal = service.Goals[commitment.GoalId];
            if (projected.GameTime > commitment.Window.End)
                return PlanStatus.NO_PATH;

            if (projected.GameTime >= commitment.Window.Start && GoalSatisfied(goal, projected))
                return CheckRemaining(index + 1, projected);

            var availableTime = horizon - projected.GameTime;
            if (availableTime <= TimeSpan.Zero || checks >= _plannerOptions.MaxScheduleChecks)
                return PlanStatus.BUDGET_EXCEEDED;

            checks++;
            var options = new PlannerOptions
            {
                MaxDepth = _plannerOptions.MaxDepth,
                MaxExpansions = _plannerOptions.MaxExpansions,
                MaxCandidatesPerAction = _plannerOptions.MaxCandidatesPerAction,
                MaxPlanDuration = availableTime,
                MaxScheduleChecks = _plannerOptions.MaxScheduleChecks
            };

            var result = ActionPlanner.FindPlan(goal, projected, service.Actions, service.FactDescriptors,
                providers, options, service.TimePredictors, new PlanConstraints
                {
                    GoalWindow = commitment.Window,
                    CheckContinuation = next => CheckRemaining(index + 1, next)
                });

            return result.Status;
        }

        return new PlanningRequest(state, providers, new PlanConstraints
        {
            GoalWindow = selection.Commitment?.Commitment.Window,
            CheckContinuation = remaining.Length == 0 ? null : CheckContinuation
        });
    }

    private bool CanContinuePlan(EntityRef citizen, MindComponent mind, TimeSpan gameTime)
    {
        if (mind.CurrentGoal == null || mind.Execution == null)
            return false;

        var selection = new GoalSelection(mind.CurrentGoal, mind.CurrentCommitment);
        var request = CreatePlanningRequest(citizen, selection, mind, gameTime);
        var projected = request.State;
        foreach (var step in mind.Execution.Plan.PlanSteps.Skip(mind.Execution.StepIndex))
        {
            if (!ActionPlanner.TryEvaluateAction(step.Candidate, projected, out var duration, out _))
                return false;

            var elapsed = TimeSpan.FromMinutes(duration);
            if (elapsed > _plannerOptions.MaxPlanDuration - (projected.GameTime - gameTime))
                return false;
            if (request.Constraints.GoalWindow is { } window &&
                (projected.GameTime > window.End || elapsed > window.End - projected.GameTime))
                return false;

            projected = ActionPredictor.Predict(step.Candidate, projected, duration,
                service.FactDescriptors, service.TimePredictors);
        }

        if (!GoalSatisfied(selection.Goal, projected))
            return false;
        if (request.Constraints.GoalWindow is { } goalWindow &&
            (projected.GameTime < goalWindow.Start || projected.GameTime > goalWindow.End))
            return false;

        var status = request.Constraints.CheckContinuation?.Invoke(projected) ?? PlanStatus.FOUND;
        return status is PlanStatus.FOUND or PlanStatus.ALREADY_SATISFIED;
    }

    private sealed record PlanningRequest(EvaluationContext State,
        IReadOnlyDictionary<string, CandidateProviderSnapshot> Providers, PlanConstraints Constraints);
    
    private void CleanupMind(EntityRef citizen, MindComponent mind)
    {
        try
        {
            if (mind.Execution?.Status == PlanExecutionStatus.RUNNING)
                executor.Cancel(citizen, mind.Execution, force: true);
        }
        finally
        {
            ClearPlan(mind);
            mind.Schedule.Clear();
            mind.GoalRetryTicks.Clear();
            mind.EmergencyActive.Clear();
        }
    }
}
