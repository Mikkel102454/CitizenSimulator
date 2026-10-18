using CitizenSimulator.Abstractions.Modding;

namespace CitizenSimulator.Job;

[Mod("job", "Job", "1.0.0", Dependencies = new[] { "calender", "vault", "citizen" })]
public class Job : IMod
{
    public void OnLoad(IModContext context)
    {

    }
}