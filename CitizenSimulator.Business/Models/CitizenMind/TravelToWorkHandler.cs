using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Models.Abstractions;

namespace CitizenSimulator.Business.Models.CitizenMind;

public sealed class TravelToWorkHandler(IEntityStore store, IBusinessService businesses) : IMindActionHandler
{
    public ActionResult Start(ActionContext context)
    {
        var shift = EmploymentActionContext.GetBoundShift(store, businesses, context);
        if (shift == null)
            return ActionResult.FAILED;

        businesses.TryLeaveWorkplace(shift.Employment.Contract.Employee);
        return ActionResult.RUNNING;
    }

    public ActionResult Tick(ActionContext context)
    {
        var shift = EmploymentActionContext.GetBoundShift(store, businesses, context);
        if (shift == null)
            return ActionResult.FAILED;

        double commuteMinutes = EmploymentActions.GetCommuteMinutes(store, context.Citizen);
        if (context.Elapsed.TotalMinutes < commuteMinutes)
            return ActionResult.RUNNING;

        return businesses.TryArriveAtWorkplace(shift.Employment.Business, shift.Employment.Contract.Employee)
            ? ActionResult.SUCCEEDED : ActionResult.FAILED;
    }

    public void Cancel(ActionContext context)
    {
        // Arrival happens only after the commute finishes; travel never earns wages.
    }
}
