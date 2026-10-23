using System.Globalization;
using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Services;
using CitizenSimulator.Abstractions.Timing;

namespace CitizenSimulator.ConsoleMod.Services;

public class ConsoleService
{
    public const string Help =
        "pause | resume | speed <multiplier> | step [count] | advance <seconds> | " +
        "until <elapsed-seconds> | status | inspect <citizen-id> | " +
        "inspect citizen|business|house|property <id> | inspect citizens|businesses|houses|properties | help | quit";

    public static bool Execute(string line, ISimulationClock clock, ISimulationControls controls, TextWriter output, IEntityStore store,
        IServiceRegistry? services, ISystemRegistry? systems = null)
    {
        var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return true;

        try
        {
            switch (parts[0].ToLowerInvariant())
            {
                case "pause" when parts.Length == 1:
                    controls.Pause();
                    break;
                case "resume" when parts.Length == 1:
                    controls.Resume();
                    break;
                case "speed" when parts.Length == 2:
                    controls.SetSpeed(double.Parse(parts[1], CultureInfo.InvariantCulture));
                    break;
                case "step" when parts.Length is 1 or 2:
                    controls.Step(parts.Length == 1 ? 1 : ulong.Parse(parts[1], CultureInfo.InvariantCulture));
                    break;
                case "advance" when parts.Length == 2:
                    controls.AdvanceBy(ParseSeconds(parts[1]));
                    break;
                case "until" when parts.Length == 2:
                    controls.AdvanceTo(ParseSeconds(parts[1]));
                    break;
                case "status" when parts.Length == 1:
                    output.WriteLine(Status(clock));
                    return true;
                case "help" when parts.Length == 1:
                    output.WriteLine(Help);
                    return true;
                case "quit" when parts.Length == 1:
                    controls.Quit();
                    return false;
                case "inspect":
                    new InspectionService(store, output, clock, services, systems).Execute(parts.Skip(1).ToArray());
                    return true;
                default:
                    output.WriteLine("Invalid command. " + Help);
                    return true;
            }
            output.WriteLine("Control requested. Use status to inspect the clock.");
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or
                                          OverflowException or InvalidOperationException)
        {
            output.WriteLine("Command error: " + exception.Message);
        }
        return true;
    }

    public static bool Execute(string line, ISimulationClock clock, ISimulationControls controls, TextWriter output, IEntityStore store) =>
        Execute(line, clock, controls, output, store, null, null);

    public static string Status(ISimulationClock clock)
    {
        var state = clock.IsAdvancing ? "advancing" : clock.IsPaused ? "paused" : "running";
        return FormattableString.Invariant($"Tick {clock.Tick} | time {clock.Elapsed.TotalSeconds:F3}s | {state} | speed {clock.Speed:G}x");
    }

    private static TimeSpan ParseSeconds(string text)
    {
        var seconds = double.Parse(text, CultureInfo.InvariantCulture);
        if (!double.IsFinite(seconds) || seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(text), "Seconds must be finite and nonnegative.");
        return TimeSpan.FromSeconds(seconds);
    }
}
