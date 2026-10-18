using CitizenSimulator.Abstractions.Modding;

namespace CitizenSimulator.CitizenBrain;

[Mod("citizen_brain", "CitizenBrain", "1.0.0", Dependencies = new[] { "needs", "vault", "calender", "job", "citizen" })]
public class CitizenBrain : IMod
{
    public void OnLoad(IModContext context)
    {

    }
}