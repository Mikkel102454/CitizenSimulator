using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Calender.Services.Abstractions;
using CitizenSimulator.Citizen.Services.Abstraction;
using CitizenSimulator.CitizenMind.Services.Abstractions;
using CitizenSimulator.Needs.Components;
using CitizenSimulator.Needs.Models.CitizenMind;
using CitizenSimulator.Needs.Services;
using CitizenSimulator.Needs.Services.Abstractions;
using CitizenSimulator.Needs.Systems;

namespace CitizenSimulator.Needs;

[Mod("needs", "Needs", "1.0.0", Dependencies = new[] { "citizen", "calender", "citizen_mind" })]
public class Needs : IMod
{
    public void OnLoad(IModContext context)
    {
        context.Services.Register<INeedsService>(new NeedsService());
        context.Systems.Register(new NeedsSystem(context.Events, context.Services.Get<ICalenderService>(), 
            context.Services.Get<ICitizenService>()));
        
        ICitizenMindService citizenMindService = context.Services.Get<ICitizenMindService>();
        
        citizenMindService.RegisterFact("needs.thirst", (entity) =>
        {
            var component = context.Store.GetComponent<NeedsComponent>(entity);
            return component?.Thirst;
        });
        
        citizenMindService.RegisterFact("needs.hunger", (entity) =>
        {
            var component = context.Store.GetComponent<NeedsComponent>(entity);
            return component?.Thirst;
        });
        
        citizenMindService.RegisterHandler("needs.drink_accessible_safe_water", new DrinkWaterHandler(context.Store, context.Services.Get<INeedsService>()));
    }
}