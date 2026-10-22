using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Business.Services;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.Business.Systems;
using CitizenSimulator.Calender.Services.Abstractions;
using CitizenSimulator.Items.Services.Abstractions;
using CitizenSimulator.Inventory.Services.Abstractions;
using CitizenSimulator.Vault.Services.Abstractions;

namespace CitizenSimulator.Business;

[Mod("business", "Business", "1.0.0", Dependencies = new[] { "items", "citizen", "inventory", "property", "vault", "calender" })]
public class Business : IMod
{
    public void OnLoad(IModContext context)
    {
        BusinessCatalog.Load(context.Services.Get<IItemService>());
        context.Services.Register<IBusinessService>(new BusinessService(
            context.Store,
            context.Events,
            context.Services.Get<IVaultService>(),
            context.Services.Get<IInventoryService>(),
            context.Services.Get<ICalenderService>(),
            context.Clock,
            context.Services.Get<IItemService>()));

        context.Systems.Register(new BusinessSystem(
            context.Services.Get<ICalenderService>(),
            context.Services.Get<IBusinessService>()));
    }
}
