using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Inventory.Services.Abstractions;
using CitizenSimulator.Market.Services;
using CitizenSimulator.Market.Services.Abstractions;
using CitizenSimulator.Market.Systems;
using CitizenSimulator.Vault.Services.Abstractions;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.Items.Services.Abstractions;
using CitizenSimulator.Calender.Services.Abstractions;
using CitizenSimulator.Market.Models.CitizenMind;

namespace CitizenSimulator.Market;

[Mod("market", "Market", "1.0.0", Dependencies = new[] { "business", "inventory", "vault", "items", "citizen_mind", "calender" })]
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
            context.Services.Get<IItemService>(),
            context.Services.Get<ICalenderService>()));

        context.Systems.Register(new BusinessRestockSystem(
            context.Services.Get<IMarketService>(),
            context.Services.Get<IBusinessService>(),
            context.Services.Get<IItemService>(), context.Services));

        context.Systems.Register(new MarketSystem(
            context.Services.Get<IMarketService>(), context.Services.Get<IBusinessService>()));
        context.Systems.Register(new CitizenDemandSystem(
            context.Services.Get<IMarketService>(), context.Services.Get<IBusinessService>(),
            context.Services.Get<IItemService>()));
        ShoppingActions.Register(context);
    }
}
