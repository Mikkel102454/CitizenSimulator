using System.Globalization;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Abstractions.Input;
using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.ConsoleMod.Systems;

namespace CitizenSimulator.ConsoleMod;

// Console behavior is a mod; the host only supplies input.
[Mod("console", "Console controls", "1.0.0", Dependencies = new[] { "citizen", "needs", "housing", "business" })]
public sealed class ConsoleMod : IMod
{
    public void OnLoad(IModContext context) => context.Systems.Register(new ConsoleSystem(context));
}

internal static class SimulationConsole
{


}
