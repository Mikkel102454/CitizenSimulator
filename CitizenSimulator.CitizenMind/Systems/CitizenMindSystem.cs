using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Calender.Services.Abstractions;
using CitizenSimulator.CitizenMind.Components;
using CitizenSimulator.CitizenMind.Configuration;
using CitizenSimulator.CitizenMind.Executor;
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
            
            if (mind.Execution != null)
            {
                if (mind.Execution.Status == PlanExecutionStatus.RUNNING)
                {
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

            var state = service.CreateEvaluationContext(entity);

            var result = ActionPlanner.FindPlan(goal, state, service.Actions, service.FactDescriptors, _plannerOptions);

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
}