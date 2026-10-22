using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Abstractions.Services;
using CitizenSimulator.Calender.Services.Abstractions;
using CitizenSimulator.Housing.Services;
using CitizenSimulator.Housing.Services.Abstractions;
using CitizenSimulator.Housing.Systems;
using CitizenSimulator.Inventory.Services.Abstractions;
using CitizenSimulator.Property.Services.Abstractions;
using CitizenSimulator.Vault.Services.Abstractions;

namespace CitizenSimulator.Housing;

[Mod("housing", "Housing", "1.0.0", Dependencies = new[] { "citizen", "property", "vault", "inventory", "calender" })]
public class Housing : IMod
{
    public void OnLoad(IModContext context)
    {
        HousingService housing = new(context.Store, context.Services.Get<IPropertyService>(),
            context.Services.Get<IVaultService>(), context.Services.Get<IInventoryService>(),
            context.Services.Get<ICalenderService>(), context.Clock);
        context.Services.Register<IHousingService>(housing);
        context.Services.Register<IHomeAccessService>(housing);
        context.Systems.Register(new HousingSystem(housing));
    }
}
