using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Models.Abstractions;

namespace CitizenSimulator.CitizenMind.Executor.Handlers;

public sealed class WaitUntilHandler : IMindActionHandler
{
    public ActionResult Start(ActionContext context) => Evaluate(context);

    public ActionResult Tick(ActionContext context) => Evaluate(context);

    public void Cancel(ActionContext context)
    {
        
    }

    private static ActionResult Evaluate(ActionContext context)
    {
        double untilMinutes = context.GetParameter<double>("untilMinutes");

        if (!double.IsFinite(untilMinutes) || untilMinutes < 0)
        {
            throw new InvalidOperationException("Waiting requires a nonnegative, finite untilMinutes parameter.");
        }
        
        return context.GameTime.TotalMinutes >= untilMinutes ? ActionResult.SUCCEEDED : ActionResult.RUNNING;
    }
}