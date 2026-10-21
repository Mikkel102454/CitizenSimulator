using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Models.Abstractions;
using CitizenSimulator.Needs.Components;
using CitizenSimulator.Needs.Services.Abstractions;

namespace CitizenSimulator.Needs.Models.CitizenMind;

public sealed class DrinkWaterHandler(IEntityStore store, INeedsService needs) : IMindActionHandler
{
    public ActionResult Start(ActionContext context)
    {
        return GetNeeds(context) is null ? ActionResult.FAILED : ActionResult.RUNNING;
    }

    public ActionResult Tick(ActionContext context)
    {
        var component = GetNeeds(context);

        if (component is null)
            return ActionResult.FAILED;

        double target = context.Parameters["targetThirst"];
        double reductionPerMinute = context.Parameters["reductionPerMinute"];
        
        double reduction = reductionPerMinute * context.Delta.TotalMinutes;

        reduction = Math.Min(reduction, Math.Max(0f, component.Thirst - target));

        if (reduction > 0 && !needs.AddThirst(component, -reduction))
            return ActionResult.FAILED;

        return component.Thirst <= target ? ActionResult.SUCCEEDED : ActionResult.RUNNING;
    }


    public void Cancel(ActionContext context)
    {
        // Nothing to clean up: thirst changes only on completion.
    }

    private NeedsComponent? GetNeeds(ActionContext context)
    {
        return store.IsAlive(context.Citizen) ? store.GetComponent<NeedsComponent>(context.Citizen) : null;
    }
}