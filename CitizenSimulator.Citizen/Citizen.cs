using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Citizen.Services;
using CitizenSimulator.Citizen.Services.Abstraction;
using CitizenSimulator.Citizen.Systems;

namespace CitizenSimulator.Citizen;

[Mod("citizen", "Citizen", "1.0.0")]
public class Citizen : IMod
{
    public void OnLoad(IModContext context)
    {
        context.Services.Register<ICitizenService>(new CitizenService(context.Store, context.Events));
        context.Systems.Register(new CitizenSystem(context.Events));
    }
}