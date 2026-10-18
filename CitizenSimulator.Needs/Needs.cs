using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Needs.Services;
using CitizenSimulator.Needs.Services.Abstractions;

namespace CitizenSimulator.Needs;

[Mod("needs", "Needs", "1.0.0", Dependencies = new[] { "citizen" })]
public class Needs : IMod
{
    public void OnLoad(IModContext context)
    {
        context.Services.Register<INeedsService>(new NeedsService(context.Store, context.Events));
    }
}