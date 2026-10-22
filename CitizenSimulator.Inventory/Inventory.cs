using CitizenSimulator.Abstractions.Modding;

using CitizenSimulator.Inventory.Models.CitizenMind;

namespace CitizenSimulator.Inventory;

[Mod("inventory", "Inventory", "1.0.0", Dependencies = new[] { "items", "citizen_mind" })]

public class Inventory : IMod
{
    public void OnLoad(IModContext context)
    {
        FoodActions.Register(context);
    }
}
