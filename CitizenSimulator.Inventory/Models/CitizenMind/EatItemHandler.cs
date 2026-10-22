using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Models.Abstractions;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Items.Models;
using CitizenSimulator.Items.Services.Abstractions;
using CitizenSimulator.Needs.Components;

namespace CitizenSimulator.Inventory.Models.CitizenMind;

public sealed class EatItemHandler(IEntityStore store, IConsumptionService consumption, Item item) : IMindActionHandler
{
    public ActionResult Start(ActionContext context) => CanEat(context) ? ActionResult.RUNNING : ActionResult.FAILED;

    public ActionResult Tick(ActionContext context)
    {
        if (!CanEat(context)) return ActionResult.FAILED;
        if (context.Elapsed.TotalMinutes < FoodActions.EatingMinutes) return ActionResult.RUNNING;
        var inventory = store.GetComponent<InventoryComponent>(context.Citizen)!;
        var citizen = store.GetComponent<CitizenComponent>(context.Citizen)!;
        decimal previous = inventory.GetQuantity(item.Id);
        inventory.Inventory[item.Id] = previous - 1;
        try
        {
            if (consumption.ConsumeItem(item, citizen)) return ActionResult.SUCCEEDED;
        }
        catch
        {
            inventory.Inventory[item.Id] = previous;
            throw;
        }
        inventory.Inventory[item.Id] = previous;
        return ActionResult.FAILED;
    }

    public void Cancel(ActionContext context) { } // Unfinished meals consume nothing.

    private bool CanEat(ActionContext context) => store.IsAlive(context.Citizen) &&
        store.GetComponent<CitizenComponent>(context.Citizen) is { Alive: true } &&
        store.GetComponent<NeedsComponent>(context.Citizen) != null &&
        store.GetComponent<InventoryComponent>(context.Citizen)?.GetQuantity(item.Id) >= 1;
}
