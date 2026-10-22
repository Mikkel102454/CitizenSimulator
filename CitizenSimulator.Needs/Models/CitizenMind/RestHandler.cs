using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Models.Abstractions;
using CitizenSimulator.Needs.Components;
using CitizenSimulator.Needs.Services.Abstractions;

namespace CitizenSimulator.Needs.Models.CitizenMind;

/// <summary>Sleeping and resting share gradual, interruptible recovery.</summary>
public sealed class RestHandler(IEntityStore store, INeedsService needs) : IMindActionHandler
{
    public ActionResult Start(ActionContext context)
    {
        var target = context.GetParameter<double>("targetEnergy");
        var rate = context.GetParameter<double>("recoveryPerMinute");
        return GetNeeds(context) != null && double.IsFinite(target) && target is > 0 and <= 1 &&
               double.IsFinite(rate) && rate > -NeedsDynamics.EnergyChangePerDay / 1440
            ? ActionResult.RUNNING : ActionResult.FAILED;
    }

    public ActionResult Tick(ActionContext context)
    {
        var component = GetNeeds(context);
        if (component == null || context.Delta < TimeSpan.Zero) return ActionResult.FAILED;
        double target = context.GetParameter<double>("targetEnergy");
        double recovery = context.GetParameter<double>("recoveryPerMinute") * context.Delta.TotalMinutes;
        recovery = Math.Min(recovery, Math.Max(0, target - component.Energy));
        if (!needs.AddEnergy(component, recovery)) return ActionResult.FAILED;
        return component.Energy >= target ? ActionResult.SUCCEEDED : ActionResult.RUNNING;
    }

    public void Cancel(ActionContext context) { } // Recovery already received is retained.

    private NeedsComponent? GetNeeds(ActionContext context) =>
        store.IsAlive(context.Citizen) && store.GetComponent<CitizenComponent>(context.Citizen) is { Alive: true }
            ? store.GetComponent<NeedsComponent>(context.Citizen) : null;
}
