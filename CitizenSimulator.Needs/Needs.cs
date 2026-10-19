using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Calender.Services.Abstractions;
using CitizenSimulator.Citizen.Services.Abstraction;
using CitizenSimulator.Needs.Services;
using CitizenSimulator.Needs.Services.Abstractions;
using CitizenSimulator.Needs.Systems;

namespace CitizenSimulator.Needs;

[Mod("needs", "Needs", "1.0.0", Dependencies = new[] { "citizen", "calender" })]
public class Needs : IMod
{
    public void OnLoad(IModContext context)
    {
        context.Services.Register<INeedsService>(new NeedsService(context.Store, context.Events));
        context.Systems.Register(new NeedsSystem(context.Events, context.Services.Get<ICalenderService>(), 
            context.Services.Get<ICitizenService>()));
    }
}