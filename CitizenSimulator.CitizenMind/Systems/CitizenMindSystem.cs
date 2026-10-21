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
using CitizenSimulator.CitizenMind.Services.Abstractions;

namespace CitizenSimulator.CitizenMind.Systems;

public class CitizenMindSystem(IEventBus events, GoalSelector goalSelector, CitizenMindService service, ICalenderService calender, ActionExecutor executor) : GameSystem
{
    private readonly PlannerOptions _plannerOptions = new();
    
    protected override void OnStart(ICommandBuffer commands)
    {
        MindDefinitionValidator.Validate(service);
    }
    
    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {
        ulong ticksPerDay = calender.GetTicksPerDay();

        TimeSpan gameDelta = TimeSpan.FromMinutes(1440.0 / ticksPerDay);

        ulong retryDelayTicks = Math.Max(1UL, (ulong)Math.Ceiling(ticksPerDay * (5.0 / 1440.0)));
        
        foreach (var entity in Store.Query(MindComponent.GetComponentType()))
        {
            MindComponent mind = Store.GetComponent<MindComponent>(entity);
            if(mind == null)
                continue;
            
            if (!mind.IsActive)
            {
                if (mind.Execution?.Status == PlanExecutionStatus.RUNNING)
                    executor.Cancel(entity, mind.Execution, force: true);

                mind.Execution = null;
                mind.CurrentGoal = null;
                mind.GoalRetryTicks.Clear();
                mind.EmergencyActive.Clear();
                
                continue;
            }
            
            if (mind.Execution != null)
            {
                if (mind.Execution.Status == PlanExecutionStatus.RUNNING)
                {
                    if (TrySwitchToEmergency(entity, mind, time.Tick, retryDelayTicks))
                        continue;
                    
                    executor.Update(entity, mind.Execution, gameDelta);
                }
                else
                {
                    
                    if (mind.Execution.Status == PlanExecutionStatus.FAILED && mind.CurrentGoal != null)
                        mind.GoalRetryTicks[mind.CurrentGoal.Id] = checked(time.Tick + retryDelayTicks);

                    mind.Execution = null;
                    mind.CurrentGoal = null;
                }

                continue;
            }
            
            var goal = goalSelector.SelectGoals(mind, time.Tick);

            if (goal == null)
            {
                mind.CurrentGoal = null;
                continue;
            }

            var result = PlanForGoal(entity, goal);

            switch (result.Status)
            {
                case PlanStatus.FOUND:
                    mind.CurrentGoal = goal;
                    mind.Execution = new PlanExecutionState
                    {
                        Plan = result.Plan ?? throw new InvalidOperationException("A found result must contain a plan.")
                    };
                    break;

                case PlanStatus.ALREADY_SATISFIED:
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
    
    private PlanResult PlanForGoal(EntityRef citizen, GoalDefinition goal)
    {
        var state = service.CreateEvaluationContext(citizen);

        var relevantActions = ActionDependencyResolver.Resolve(goal, service.Actions);

        var providers = service.CreateCandidateProviders(citizen, relevantActions);

        return ActionPlanner.FindPlan(goal, state, service.Actions, service.FactDescriptors, providers, _plannerOptions);
    }
    
    private bool TrySwitchToEmergency(EntityRef citizen, MindComponent mind, ulong currentTick, ulong retryDelayTicks)
    {
        var emergency = goalSelector.SelectGoals(mind, currentTick, emergenciesOnly: true);

        if (emergency == null || emergency.Id == mind.CurrentGoal?.Id)
            return false;

        var execution = mind.Execution;

        if (execution == null || execution.Status != PlanExecutionStatus.RUNNING)
            return false;

        // an equally or more urgent running goal keeps its plan.
        var currentEmergency = mind.CurrentGoal?.Emergency;

        if (currentEmergency != null)
        {
            var live = service.CreateEvaluationContext(citizen);

            if (ExpressionEvaluator.Evaluate(live, currentEmergency.When) && currentEmergency.Priority >= emergency.Emergency!.Priority)
                return false;
        }

        if (execution.Started)
        {
            var action = execution.Plan.PlanSteps[execution.StepIndex].Candidate.Definition;

            if (!action.Execution.Interruptible)
                return false;
        }

        var result = PlanForGoal(citizen, emergency);

        if (result.Status == PlanStatus.NO_PATH || result.Status == PlanStatus.BUDGET_EXCEEDED)
        {
            mind.GoalRetryTicks[emergency.Id] = checked(currentTick + retryDelayTicks);

            return false;
        }

        if (result.Status != PlanStatus.FOUND)
            return false;

        var plan = result.Plan ?? throw new InvalidOperationException("A found emergency result must contain a plan.");

        if (!executor.Cancel(citizen, execution))
            return false;

        mind.CurrentGoal = emergency;
        mind.Execution = new PlanExecutionState
        {
            Plan = plan
        };

        return true;
    }
}