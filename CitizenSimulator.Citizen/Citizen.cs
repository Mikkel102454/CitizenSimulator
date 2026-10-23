using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Citizen.Services;
using CitizenSimulator.Citizen.Services.Abstraction;
using CitizenSimulator.Citizen.Systems;

namespace CitizenSimulator.Citizen;

[Mod("citizen", "Citizen", "1.0.0", Dependencies = new[] { "calender", "citizen_mind" })]
public class Citizen : IMod
{
    public void OnLoad(IModContext context)
    {
        CitizenService citizenService = new CitizenService(context.Store, context.Events);
        context.Services.Register<ICitizenService>(citizenService);
        context.Systems.Register(new CitizenSystem(context.Events));
    }
}
