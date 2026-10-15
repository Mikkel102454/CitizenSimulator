using CitizenSimulator.TestMod.Services;
using CitizenSimulator.Abstractions;
using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.TestMod.Components;
using CitizenSimulator.TestMod.Systems;

namespace CitizenSimulator.TestMod;

//This would make the plugin depend on another plugin. reference by id
//[Mod("test", "Test", "1.0.0", Dependencies = new[] { "test2" })]
[Mod("test", "Test", "1.0.0")]
public sealed class TestMod : IMod
{
    public void OnLoad(IModContext context)
    {
        context.Services.Register<IGreetingService>(new GreetingService());

        // system register automatically 
        // register system
        context.Systems.Register(new MessageSystem(context.Events));

        // create entity
        var entity = context.Store.CreateEntity();

        // attach component
        context.Store.AddComponent(entity,
            new MessageComponent
            {
                Message = "Yooo this works"
            });
    }
}
