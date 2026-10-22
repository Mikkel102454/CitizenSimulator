using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Inventory.Services.Abstractions;
using CitizenSimulator.Market.Services;
using CitizenSimulator.Market.Services.Abstractions;
using CitizenSimulator.Market.Systems;
using CitizenSimulator.Vault.Services.Abstractions;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.Items.Services.Abstractions;

namespace CitizenSimulator.Market;

[Mod("market", "Market", "1.0.0", Dependencies = new[] { "business", "inventory", "vault" })]
public class Market : IMod
{
    public void OnLoad(IModContext context)
    {
        context.Services.Register<IMarketService>(new MarketService(
            context.Store,
            context.Events,
            context.Services.Get<IVaultService>(),
            context.Services.Get<IInventoryService>(),
            context.Services.Get<IBusinessService>(),
            context.Services.Get<IItemService>()));

        context.Systems.Register(new BusinessRestockSystem(
            context.Services.Get<IMarketService>(),
            context.Services.Get<IBusinessService>(),
            context.Services.Get<IItemService>()));
    }
}
