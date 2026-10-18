using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Calender.Services;
using CitizenSimulator.Calender.Systems;

namespace CitizenSimulator.Calender;

[Mod("calender", "Calender", "1.0.0")]
public class Calender : IMod
{
    public void OnLoad(IModContext context)
    {
        CalenderService calenderService = new CalenderService(context.Store, context.Events);
        
        context.Services.Register(calenderService);
        context.Systems.Register(new CalenderSystem(context.Events, calenderService));

    }
}