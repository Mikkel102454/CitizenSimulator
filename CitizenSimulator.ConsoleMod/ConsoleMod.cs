using System.Globalization;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Abstractions.Input;
using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.ConsoleMod;

// Console behavior is a mod; the host only supplies input.
[Mod("console", "Console controls", "1.0.0")]
public sealed class ConsoleMod : IMod
{
    public void OnLoad(IModContext context) => context.Systems.Register(new ConsoleSystem(context));
}
internal sealed class ConsoleSystem(IModContext context) : GameSystem
{
    private IDisposable? _line;
    private IDisposable? _error;
    protected override void OnStart(ICommandBuffer commands)
    {
        Console.WriteLine(SimulationConsole.Help);
        _line = context.InputEvents.Subscribe<LineSubmitted>((line, time, buffer) =>
            SimulationConsole.Execute(line.Text, context.Clock, context.Controls, Console.Out));
        _error = context.InputEvents.Subscribe<ControlFailed>((error, time, buffer) =>
            Console.WriteLine("Control error: " + error.Message));
    }
    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands) { }
    protected override void OnStop(ICommandBuffer commands) { _line?.Dispose(); _error?.Dispose(); }
}
internal static class SimulationConsole
{
    public const string Help =
        "pause | resume | speed <multiplier> | step [count] | advance <seconds> | " +
        "until <elapsed-seconds> | status | help | quit";

    public static bool Execute(string line, ISimulationClock clock, ISimulationControls controls, TextWriter output)
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
