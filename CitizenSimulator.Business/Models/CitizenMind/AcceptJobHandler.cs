using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Models.Abstractions;

namespace CitizenSimulator.Business.Models.CitizenMind;

public sealed class AcceptJobHandler(IEntityStore store, IBusinessService businesses) : IMindActionHandler
{
    public ActionResult Start(ActionContext context)
    {
        return CanApply(context) ? ActionResult.RUNNING : ActionResult.FAILED;
    }

    public ActionResult Tick(ActionContext context)
    {
        if (!CanApply(context))
            return ActionResult.FAILED;

        if (context.Elapsed.TotalMinutes < context.GetParameter<double>("durationMinutes"))
            return ActionResult.RUNNING;

        var worker = EmploymentActionContext.GetWorker(store, context)!;
        var employer = EmploymentActionContext.GetEmployer(store, context)!;
        string role = (string)context.Bindings["role"]!;
        decimal wage = (decimal)context.Bindings["hourlyWage"]!;
        return businesses.TryHire(employer, worker, role, out _, wage)
            ? ActionResult.SUCCEEDED : ActionResult.FAILED;
    }

    public void Cancel(ActionContext context)
    {
        // An interrupted application does not create a contract.
    }

    private bool CanApply(ActionContext context)
    {
        var worker = EmploymentActionContext.GetWorker(store, context);
        var employer = EmploymentActionContext.GetEmployer(store, context);
        if (worker == null || employer == null
            || !context.Bindings.TryGetValue("role", out var roleValue) || roleValue is not string role
            || !context.Bindings.TryGetValue("hourlyWage", out var wageValue) || wageValue is not decimal wage)
            return false;

        return businesses.GetAvailableJobs(worker).Any(vacancy =>
            ReferenceEquals(vacancy.Business, employer) && vacancy.Role == role && vacancy.HourlyWage == wage);
    }
}
