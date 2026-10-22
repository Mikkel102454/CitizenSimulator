using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Models.Abstractions;

namespace CitizenSimulator.Business.Models.CitizenMind;

public sealed class WorkShiftHandler(IEntityStore store, IBusinessService businesses) : IMindActionHandler
{
    public ActionResult Start(ActionContext context)
    {
        var shift = EmploymentActionContext.GetBoundShift(store, businesses, context);
        if (shift == null || shift.Start > businesses.GameTime)
            return ActionResult.FAILED;

        var employment = shift.Employment;
        bool checkedIn = employment.Contract.IsCheckedIn
            || businesses.TryCheckIn(employment.Business, employment.Contract.Employee);
        return checkedIn ? ActionResult.RUNNING : ActionResult.FAILED;
    }

    public ActionResult Tick(ActionContext context)
    {
        var employment = EmploymentActionContext.GetBoundEmployment(store, businesses, context);
        if (employment == null
            || !context.Bindings.TryGetValue("shiftEndMinutes", out var value) || value is not double shiftEnd)
            return ActionResult.FAILED;

        if (businesses.GameTime.TotalMinutes >= shiftEnd)
        {
            businesses.TryLeaveWorkplace(employment.Contract.Employee);
            return ActionResult.SUCCEEDED;
        }

        return employment.Contract.IsCheckedIn ? ActionResult.RUNNING : ActionResult.FAILED;
    }

    public void Cancel(ActionContext context)
    {
        var employment = EmploymentActionContext.GetBoundEmployment(store, businesses, context);
        if (employment != null)
            businesses.TryLeaveWorkplace(employment.Contract.Employee);
    }
}
