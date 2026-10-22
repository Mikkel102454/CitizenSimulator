using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Abstractions.Services;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.Calender.Services.Abstractions;
using CitizenSimulator.Items.Services.Abstractions;
using CitizenSimulator.Market.Services.Abstractions;
using CitizenSimulator.Utilities.Services;
using CitizenSimulator.Utilities.Services.Abstractions;
using CitizenSimulator.Utilities.Systems;
using CitizenSimulator.Vault.Services.Abstractions;

namespace CitizenSimulator.Utilities;

[Mod("utilities", "Utilities and world supply", "1.0.0", Dependencies = new[] { "business", "market", "housing", "items", "vault", "calender" })]
public sealed class Utilities : IMod
{
    public void OnLoad(IModContext context)
    {
        string directory = Path.GetDirectoryName(typeof(Utilities).Assembly.Location)!;
        string overridePath = Path.Combine(AppContext.BaseDirectory, "Configuration", "world-supply.yml");
        string path = File.Exists(overridePath) ? overridePath : Path.Combine(directory, "Configuration", "world-supply.yml");
        var businesses = context.Services.Get<IBusinessService>();
        var items = context.Services.Get<IItemService>();
        var configuration = SupplyConfigurationLoader.Load(path, items, businesses);
        UtilityService utilities = new(context.Store, businesses, context.Services.Get<IMarketService>(), items,
            context.Services.Get<IVaultService>(), context.Services.Get<ICalenderService>(), configuration);
        context.Services.Register<IUtilityService>(utilities);
        context.Services.Register<ICitySupplyService>(utilities);
        context.Services.Register<IHomeUtilityService>(utilities);
        context.Systems.Register(new UtilitySystem(utilities));
        context.Systems.Register(new HomeUtilitySystem(utilities));
    }
}
