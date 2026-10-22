using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.CitizenMind.Models;

namespace CitizenSimulator.Business.Models.CitizenMind;

internal static class EmploymentActionContext
{
    public static CitizenComponent? GetWorker(IEntityStore store, ActionContext context)
    {
        if (!store.IsAlive(context.Citizen))
            return null;

        return store.GetComponent<CitizenComponent>(context.Citizen) is { Alive: true } worker ? worker : null;
    }

    public static BusinessComponent? GetEmployer(IEntityStore store, ActionContext context)
    {
        if (!context.Bindings.TryGetValue("employer", out var value)
            || value is not EntityRef employer || !store.IsAlive(employer))
            return null;

        return store.GetComponent<BusinessComponent>(employer);
    }

    public static Employment? GetBoundEmployment(IEntityStore store, IBusinessService businesses, ActionContext context)
    {
        var worker = GetWorker(store, context);
        var employer = GetEmployer(store, context);
        if (worker == null || employer == null
            || !context.Bindings.TryGetValue("contractId", out var value) || value is not Guid contractId)
            return null;

        var employment = businesses.GetEmployment(worker);
        return employment != null && ReferenceEquals(employment.Business, employer)
            && employment.Contract.Id == contractId ? employment : null;
    }

    public static WorkShift? GetBoundShift(IEntityStore store, IBusinessService businesses, ActionContext context)
    {
        var employment = GetBoundEmployment(store, businesses, context);
        if (employment == null || !context.Bindings.TryGetValue("shiftEndMinutes", out var value)
            || value is not double shiftEnd || !context.Bindings.TryGetValue("shiftStartMinutes", out var startValue)
            || startValue is not double shiftStart)
            return null;

        return businesses.GetUpcomingShifts(employment.Contract.Employee)
            .FirstOrDefault(shift => shift.End.TotalMinutes == shiftEnd && shift.Start.TotalMinutes == shiftStart);
    }
}
