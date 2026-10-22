using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.CitizenMind.Models.Abstractions;
using CitizenSimulator.Needs.Components;
using CitizenSimulator.Needs.Services.Abstractions;

namespace CitizenSimulator.Needs.Models.CitizenMind;

public sealed class DrinkWaterHandler(IEntityStore store, INeedsService needs,
    Func<EntityRef, bool>? canAccess = null, Func<EntityRef, decimal, decimal>? consumeWater = null) : IMindActionHandler
{
    public ActionResult Start(ActionContext context)
    {
        double target = context.GetParameter<double>("targetThirst");
        double rate = context.GetParameter<double>("reductionPerMinute");
        return GetNeeds(context) != null && double.IsFinite(target) && target is >= 0 and <= 1
            && double.IsFinite(rate) && rate > NeedsDynamics.ThirstChangePerDay / 1440
            ? ActionResult.RUNNING : ActionResult.FAILED;
    }

    public ActionResult Tick(ActionContext context)
    {
        var component = GetNeeds(context);

        if (component is null || !double.IsFinite(component.Thirst) || context.Delta < TimeSpan.Zero)
            return ActionResult.FAILED;
        
        double target = context.GetParameter<double>("targetThirst");
        double reductionPerMinute = context.GetParameter<double>("reductionPerMinute");
        if (!double.IsFinite(target) || target is < 0 or > 1
            || !double.IsFinite(reductionPerMinute) || reductionPerMinute <= 0)
            return ActionResult.FAILED;
        
        double reduction = reductionPerMinute * context.Delta.TotalMinutes;

        reduction = Math.Min(reduction, Math.Max(0f, component.Thirst - target));

        // A quarter litre per active drinking minute; stop consuming at the thirst target.
        decimal litres = (decimal)(reduction / reductionPerMinute) * 0.25m;
        if (litres > 0 && consumeWater != null)
        {
            decimal consumed = consumeWater(context.Citizen, litres);
            if (consumed <= 0 || consumed > litres)
                return ActionResult.FAILED;

            reduction *= (double)(consumed / litres);
        }

        if (reduction > 0 && !needs.AddThirst(component, -reduction))
            return ActionResult.FAILED;

        return component.Thirst <= target ? ActionResult.SUCCEEDED : ActionResult.RUNNING;
    }


    public void Cancel(ActionContext context)
    {
        // Thirst already reduced during the action is retained.
    }

    private NeedsComponent? GetNeeds(ActionContext context)
    {
        if (!store.IsAlive(context.Citizen) || store.GetComponent<CitizenComponent>(context.Citizen) is not { Alive: true }
            || (canAccess != null && !canAccess(context.Citizen)))
            return null;

        return store.GetComponent<NeedsComponent>(context.Citizen);
    }
}
