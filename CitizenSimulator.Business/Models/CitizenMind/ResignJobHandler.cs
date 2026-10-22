using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Models.Abstractions;

namespace CitizenSimulator.Business.Models.CitizenMind;

public sealed class ResignJobHandler(IEntityStore store, IBusinessService businesses) : IMindActionHandler
{
    public ActionResult Start(ActionContext context)
    {
        return HasUnpaidSuspendedJob(context) ? ActionResult.RUNNING : ActionResult.FAILED;
    }

    public ActionResult Tick(ActionContext context)
    {
        if (!HasUnpaidSuspendedJob(context))
            return ActionResult.FAILED;

        if (context.Elapsed.TotalMinutes < context.GetParameter<double>("durationMinutes"))
            return ActionResult.RUNNING;

        var worker = EmploymentActionContext.GetWorker(store, context)!;
        return businesses.TryResign(worker) ? ActionResult.SUCCEEDED : ActionResult.FAILED;
    }

    public void Cancel(ActionContext context)
    {
    }

    private bool HasUnpaidSuspendedJob(ActionContext context)
    {
        var worker = EmploymentActionContext.GetWorker(store, context);
        var employment = worker == null ? null : businesses.GetEmployment(worker);
        return employment is { Business.State: BusinessState.Suspended, Contract.UnpaidWages: > 0 };
    }
}
