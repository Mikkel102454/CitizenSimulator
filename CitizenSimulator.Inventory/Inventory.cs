using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Inventory.Models.CitizenMind;
using CitizenSimulator.Inventory.Services;
using CitizenSimulator.Inventory.Services.Abstractions;
using CitizenSimulator.Items.Services.Abstractions;
using CitizenSimulator.Inventory.Systems;

namespace CitizenSimulator.Inventory;

[Mod("inventory", "Inventory", "1.0.0", Dependencies = new[] { "items", "citizen_mind" })]
public class Inventory : IMod
{
    public void OnLoad(IModContext context)
    {
        context.Services.Register<IInventoryService>(new InventoryService(
            context.Store,
            context.Events,
            context.Services.Get<IConsumptionService>(),
            context.Services.Get<IItemService>()));

        FoodActions.Register(context);
        context.Systems.Register(new CitizenInventorySystem(context.Events));
    }
}
