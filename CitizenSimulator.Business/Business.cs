using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Business.Services;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.Business.Systems;
using CitizenSimulator.Items.Services.Abstractions;
using CitizenSimulator.Vault.Services;
using CitizenSimulator.Vault.Services.Abstractions;

namespace CitizenSimulator.Business;

[Mod("business", "Business", "1.0.0", Dependencies = new[] { "items", "citizen", "inventory", "property", "vault" })]
public class Business : IMod
{
    public void OnLoad(IModContext context)
    {
        context.Services.Register<IBusinessService>(new BusinessService(context.Store, context.Events, context.Services.Get<IVaultService>()));
        context.Systems.Register(new BusinessSystem(context.Services.Get<IItemService>()));
    }
}
