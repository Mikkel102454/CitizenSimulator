using CitizenSimulator.Abstractions;
using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Mod2.Systems;

namespace CitizenSimulator.Mod2;

//This would make the plugin depend on another plugin. reference by id
//[Mod("test", "Test", "1.0.0", Dependencies = new[] { "test2" })]
[Mod("test2", "Test2", "1.0.0", Dependencies = new[] { "test" })]
public sealed class Mod2 : IMod
{
    public void OnLoad(IModContext context)
    {
        context.Systems.Register(new Mod2System(context.Events));
    }
}