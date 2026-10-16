using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Human.Services;
using CitizenSimulator.Human.Services.Abstraction;
using CitizenSimulator.Human.Systems;

namespace CitizenSimulator.Human;

[Mod("test", "Test", "1.0.0")]
public class Human
{
    public void OnLoad(IModContext context)
    {
        context.Services.Register<IHumanService>(new HumanService(context.Store, context.Events));
        context.Systems.Register(new HumanSystem(context.Events));
    }
}