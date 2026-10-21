using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Services;

namespace CitizenSimulator.CitizenMind.Executor;

public class ActionExecutor(CitizenMindService service)
{
    private ActionResult StartStep(EntityRef citizen, PlanExecutionState execution)
    {
        if (execution.StepIndex < 0 || execution.StepIndex >= execution.Plan.PlanSteps.Count)
        {
            throw new InvalidOperationException("Invalid execution step index.");
        }

        if (execution.Started)
            throw new InvalidOperationException("The step has already started.");

        var step = execution.Plan.PlanSteps[execution.StepIndex];
        var action = step.Action;

        if (!service.Handler.TryGetValue(action.Handler, out var handler))
            throw new InvalidOperationException($"Unknown action handler '{action.Handler}'.");

        var live = service.CreateEvaluationContext(citizen);

        var context = new EvaluationContext
        {
            Facts = live.Facts,
            Parameters = action.Parameters
        };

        foreach (var precondition in action.Preconditions)
        {
            if (!ExpressionEvaluator.Evaluate(context, precondition))
                return ActionResult.FAILED;
        }

        execution.Before = new Dictionary<string, object?>(live.Facts, live.Facts.Comparer);

        execution.Elapsed = TimeSpan.Zero;
        execution.Started = true;

        var actionContext = new ActionContext(citizen, TimeSpan.Zero, TimeSpan.Zero)
        {
            Parameters = action.Parameters
        };

        return handler.Start(actionContext);
    }
    
    private bool IsStepComplete(EntityRef citizen, PlanExecutionState execution)
    {
        var step = execution.Plan.PlanSteps[execution.StepIndex];
        var live = service.CreateEvaluationContext(citizen);

        var context = new EvaluationContext
        {
            Facts = live.Facts,
            Before = execution.Before,
            Parameters = step.Action.Parameters,
            Calculated = new Dictionary<string, double>
            {
                ["durationMinutes"] = step.DurationMinutes
            }
        };

        foreach (var condition in step.Action.Completion)
        {
            if (!ExpressionEvaluator.Evaluate(context, condition))
                return false;
        }

        return true;
    }
    
    private void HandleResult(EntityRef citizen, PlanExecutionState execution, ActionResult result)
    {
        switch (result)
        {
            case ActionResult.RUNNING:
                return;

            case ActionResult.FAILED:
                execution.Status = PlanExecutionStatus.FAILED;
                return;

            case ActionResult.SUCCEEDED:
                if (!IsStepComplete(citizen, execution))
                {
                    execution.Status = PlanExecutionStatus.FAILED;
                    return;
                }

                execution.StepIndex++;
                execution.Started = false;
                execution.Elapsed = TimeSpan.Zero;
                execution.Before.Clear();

                if (execution.StepIndex >= execution.Plan.PlanSteps.Count)
                    execution.Status = PlanExecutionStatus.COMPLETED;

                return;

            default:
                throw new InvalidOperationException($"Unknown action result '{result}'.");
        }
    }
    
    public void Update(EntityRef citizen, PlanExecutionState execution, TimeSpan gameDelta)
    {
        if (gameDelta < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(gameDelta));

        if (execution.Status != PlanExecutionStatus.RUNNING)
            return;

        if (!execution.Started)
        {
            var startResult = StartStep(citizen, execution);
            HandleResult(citizen, execution, startResult);
            return;
        }

        if (execution.StepIndex < 0 || execution.StepIndex >= execution.Plan.PlanSteps.Count)
        {
            throw new InvalidOperationException("Invalid execution step index.");
        }

        var action = execution.Plan.PlanSteps[execution.StepIndex].Action;

        if (!service.Handler.TryGetValue(action.Handler, out var handler))
            throw new InvalidOperationException($"Unknown action handler '{action.Handler}'.");

        execution.Elapsed += gameDelta;

        var context = new ActionContext(citizen, execution.Elapsed, gameDelta)
        {
            Parameters = action.Parameters
        };

        var result = handler.Tick(context);
        HandleResult(citizen, execution, result);
    }
    
    public bool Cancel(EntityRef citizen, PlanExecutionState execution, bool force = false)
    {
        if (execution.Status != PlanExecutionStatus.RUNNING)
            return false;

        if (!execution.Started)
        {
            execution.Status = PlanExecutionStatus.CANCELLED;
            return true;
        }

        var action = execution.Plan.PlanSteps[execution.StepIndex].Action;

        if (!force && !action.Execution.Interruptible)
            return false;

        if (!service.Handler.TryGetValue(action.Handler, out var handler))
            throw new InvalidOperationException($"Unknown action handler '{action.Handler}'.");

        var context = new ActionContext(citizen, execution.Elapsed, TimeSpan.Zero)
        {
            Parameters = action.Parameters
        };

        execution.Status = PlanExecutionStatus.CANCELLED;

        try
        {
            handler.Cancel(context);
        }
        finally
        {
            execution.Started = false;
        }

        return true;
    }
}