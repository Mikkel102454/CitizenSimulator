using CitizenSimulator.Abstractions.Modding;

namespace CitizenSimulator.Market;

[Mod("market", "Market", "1.0.0", Dependencies = new[] { "business", "inventory", "vault" })]
public class Market : IMod
{
    public void OnLoad(IModContext context)
    {

    }
}