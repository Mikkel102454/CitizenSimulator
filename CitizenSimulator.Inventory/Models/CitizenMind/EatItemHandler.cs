using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Models.Abstractions;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Inventory.Services.Abstractions;
using CitizenSimulator.Items.Models;
using CitizenSimulator.Items.Services.Abstractions;
using CitizenSimulator.Needs.Components;

namespace CitizenSimulator.Inventory.Models.CitizenMind;

public sealed class EatItemHandler(IEntityStore store,
    IInventoryService inventoryService,
    Item item) : IMindActionHandler
{
    public ActionResult Start(ActionContext context)
    {
        return CanEat(context) ? ActionResult.RUNNING : ActionResult.FAILED;
    }

    public ActionResult Tick(ActionContext context)
    {
        if (!CanEat(context))
            return ActionResult.FAILED;

        if (context.Elapsed.TotalMinutes < context.GetParameter<double>("durationMinutes"))
            return ActionResult.RUNNING;

        var inventory = store.GetComponent<InventoryComponent>(context.Citizen)!;
        return inventoryService.TryConsume(item, inventory) ? ActionResult.SUCCEEDED : ActionResult.FAILED;
    }

    public void Cancel(ActionContext context)
    {
    } // Unfinished meals consume nothing.

    private bool CanEat(ActionContext context)
    {
        return store.IsAlive(context.Citizen)
            && store.GetComponent<CitizenComponent>(context.Citizen) is { Alive: true }
            && store.GetComponent<NeedsComponent>(context.Citizen) != null
            && store.GetComponent<InventoryComponent>(context.Citizen)?.GetQuantity(item.Id) >= 1;
    }
}
