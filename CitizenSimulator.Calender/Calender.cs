using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Calender.Systems;

namespace CitizenSimulator.Calender;

[Mod("calender", "Calender", "1.0.0")]
public class Calender : IMod
{
    public void OnLoad(IModContext context)
    {
        context.Systems.Register(new CalenderSystem(context.Events));

    }
}