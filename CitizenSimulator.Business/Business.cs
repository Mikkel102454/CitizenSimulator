using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Business.Systems;
using CitizenSimulator.Items.Services.Abstractions;

namespace CitizenSimulator.Business;

[Mod("business", "Business", "1.0.0", Dependencies = new[] { "items" })]
public class Business : IMod
{
    public void OnLoad(IModContext context)
    {
        context.Systems.Register(new BusinessSystem(context.Services.Get<IItemService>()));
    }
}
