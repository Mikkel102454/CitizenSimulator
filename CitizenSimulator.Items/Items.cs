using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Items.Services;
using CitizenSimulator.Items.Services.Abstractions;
using CitizenSimulator.Items.Systems;
using CitizenSimulator.Needs.Services;
using CitizenSimulator.Needs.Services.Abstractions;

namespace CitizenSimulator.Items;

[Mod("items", "Items", "1.0.0", Dependencies = new[] { "citizen", "needs" })]
public class Items : IMod
{
    public void OnLoad(IModContext context)
    {
        context.Services.Register<IConsumptionService>(new ConsumptionService(context.Store, context.Events, context.Services.Get<INeedsService>()));
        context.Services.Register<IItemService>(new ItemService(context.Store, context.Events));
        context.Systems.Register(new ItemsSystem(context.Events));
    }
}



