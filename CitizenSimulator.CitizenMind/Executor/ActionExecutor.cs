using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Planning;
using CitizenSimulator.CitizenMind.Services;

namespace CitizenSimulator.CitizenMind.Executor;

public class ActionExecutor(CitizenMindService service)
{
    private ActionResult StartStep(EntityRef citizen, PlanExecutionState execution, TimeSpan gameTime)
    {
        if (execution.StepIndex < 0 || execution.StepIndex >= execution.Plan.PlanSteps.Count)
        {
            throw new InvalidOperationException("Invalid execution step index.");
        }

        if (execution.Started)
            throw new InvalidOperationException("The step has already started.");
        
        var step = execution.Plan.PlanSteps[execution.StepIndex];
        var candidate = step.Candidate;
        var action = candidate.Definition;

        if (!service.Handler.TryGetValue(action.Handler, out var handler))
            throw new InvalidOperationException($"Unknown action handler '{action.Handler}'.");

        var live = service.CreateEvaluationContext(citizen, gameTime);
        
        if (!ActionPlanner.TryEvaluateAction(candidate, live, out _, out _))
            return ActionResult.FAILED;

        execution.Before = new Dictionary<string, object?>(live.Facts, live.Facts.Comparer);

        execution.Elapsed = TimeSpan.Zero;
        execution.Started = true;

        var actionContext = new ActionContext(citizen, TimeSpan.Zero, TimeSpan.Zero)
        {
            Parameters = action.ResolvedParameters,
            Bindings = candidate.Bindings,
            GameTime = gameTime
        };

        return handler.Start(actionContext);
    }
    
    private bool IsStepComplete(EntityRef citizen, PlanExecutionState execution, TimeSpan gameTime)
    {
        var step = execution.Plan.PlanSteps[execution.StepIndex];
        var candidate = step.Candidate;
        var action = candidate.Definition;
        var live = service.CreateEvaluationContext(citizen, gameTime);

        var context = new EvaluationContext
        {
            Facts = live.Facts,
            Before = execution.Before,
            Parameters = action.ResolvedParameters,
            Bindings = candidate.Bindings,
            GameTime = live.GameTime,
            Calculated = new Dictionary<string, double>
            {
                ["durationMinutes"] = step.DurationMinutes
            }
        };

        foreach (var condition in action.Completion)
        {
            if (!ExpressionEvaluator.Evaluate(context, condition))
                return false;
        }

        return true;
    }
    
    private void HandleResult(EntityRef citizen, PlanExecutionState execution, ActionResult result, TimeSpan gameTime)
    {
        switch (result)
        {
            case ActionResult.RUNNING:
                return;

            case ActionResult.FAILED:
                FailExecution(citizen, execution);
                return;

            case ActionResult.SUCCEEDED:
                if (!IsStepComplete(citizen, execution, gameTime))
                {
                    FailExecution(citizen, execution);
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
    
    public void Update(EntityRef citizen, PlanExecutionState execution, TimeSpan gameDelta, TimeSpan gameTime)
    {
        if (gameDelta < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(gameDelta));

        if (execution.Status != PlanExecutionStatus.RUNNING)
            return;

        if (!execution.Started)
        {
            var startResult = StartStep(citizen, execution, gameTime);
            HandleResult(citizen, execution, startResult, gameTime);
            return;
        }

        if (execution.StepIndex < 0 || execution.StepIndex >= execution.Plan.PlanSteps.Count)
        {
            throw new InvalidOperationException("Invalid execution step index.");
        }

        
        var step = execution.Plan.PlanSteps[execution.StepIndex];
        var candidate = step.Candidate;
        var action = candidate.Definition;

        if (!service.Handler.TryGetValue(action.Handler, out var handler))
            throw new InvalidOperationException($"Unknown action handler '{action.Handler}'.");

        TimeSpan stepDelta = gameDelta;
        bool timeoutReached = false;

        if (action.Execution.TimeoutMinutes is double timeoutMinutes)
        {
            TimeSpan limit =
                DurationConverter.ToPositiveTimeSpan(timeoutMinutes);

            TimeSpan remaining = limit - execution.Elapsed;

            if (remaining <= TimeSpan.Zero)
            {
                FailExecution(citizen, execution);
                return;
            }

            if (stepDelta >= remaining)
            {
                stepDelta = remaining;
                timeoutReached = true;
            }
        }

        execution.Elapsed += stepDelta;

        var context = new ActionContext(citizen, execution.Elapsed, stepDelta)
        {
            Parameters = action.ResolvedParameters,
            Bindings = candidate.Bindings,
            GameTime = gameTime
        };

        var result = handler.Tick(context);

        if (result == ActionResult.RUNNING && timeoutReached)
        {
            FailExecution(citizen, execution);
            return;
        }

        HandleResult(citizen, execution, result, gameTime);
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
        
        var step = execution.Plan.PlanSteps[execution.StepIndex];
        var candidate = step.Candidate;
        var action = candidate.Definition;

        if (!force && !action.Execution.Interruptible)
            return false;

        if (!service.Handler.TryGetValue(action.Handler, out var handler))
            throw new InvalidOperationException($"Unknown action handler '{action.Handler}'.");

        var context = new ActionContext(citizen, execution.Elapsed, TimeSpan.Zero)
        {
            Parameters = action.ResolvedParameters,
            Bindings = candidate.Bindings,
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
    
    private void FailExecution(EntityRef citizen, PlanExecutionState execution)
    {
        try
        {
            Cancel(citizen, execution, force: true);
        }
        finally
        {
            execution.Status = PlanExecutionStatus.FAILED;
        }
    }
}