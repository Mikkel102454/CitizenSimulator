using CitizenSimulator.Abstractions.Modding;

namespace CitizenSimulator.Inventory;

[Mod("inventory", "Inventory", "1.0.0", Dependencies = new[] { "items" })]

public class Inventory : IMod
{
    public void OnLoad(IModContext context)
    {
        
    }
}