using System.Globalization;
using CitizenSimulator.Engine.Timing;

namespace CitizenSimulator;

// comands are interpreted on the command thread between tick batches
public static class SimulationConsole
{
    public const string Help =
        "pause | resume | speed <multiplier> | step [count] | advance <seconds> | " +
        "until <elapsed-seconds> | status | help | quit";

    //false when we should quit
    public static bool Execute(string line, SimulationClock clock, TextWriter output)
    {
        var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return true;

        try
        {
            switch (parts[0].ToLowerInvariant())
            {
                case "pause" when parts.Length == 1:
                    clock.Pause();
                    break;
                case "resume" when parts.Length == 1:
                    clock.Resume();
                    break;
                case "speed" when parts.Length == 2:
                    clock.SetSpeed(double.Parse(parts[1], CultureInfo.InvariantCulture));
                    break;
                case "step" when parts.Length is 1 or 2:
                    clock.Step(parts.Length == 1 ? 1 : ulong.Parse(parts[1], CultureInfo.InvariantCulture));
                    break;
                case "advance" when parts.Length == 2:
                    clock.AdvanceBy(ParseSeconds(parts[1]));
                    break;
                case "until" when parts.Length == 2:
                    clock.AdvanceTo(ParseSeconds(parts[1]));
                    break;
                case "status" when parts.Length == 1:
                    output.WriteLine(Status(clock));
                    return true;
                case "help" when parts.Length == 1:
                    output.WriteLine(Help);
                    return true;
                case "quit" when parts.Length == 1:
                    return false;
                default:
                    output.WriteLine("Invalid command. " + Help);
                    return true;
            }
            output.WriteLine(Status(clock));
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or
                                          OverflowException or InvalidOperationException)
        {
            output.WriteLine("Command error: " + exception.Message);
        }
        return true;
    }

    public static string Status(SimulationClock clock)
    {
        var d = clock.Diagnostics;
        var state = clock.IsAdvancing ? "advancing" : clock.IsPaused ? "paused" : "running";
        var warning = d.IsOverloaded ? " | OVERLOADED: requested speed cannot currently be achieved" : "";
        return FormattableString.Invariant($"Tick {clock.Tick} | time {clock.Elapsed.TotalSeconds:F3}s | {state} | speed {clock.Speed:G}x | {d.AchievedTicksPerSecond:F1} ticks/s | cost avg/max {d.AverageTickCost.TotalMilliseconds:F3}/{d.MaximumTickCost.TotalMilliseconds:F3}ms | backlog {d.PendingTicks:F2}{warning}");
    }

    private static TimeSpan ParseSeconds(string text)
    {
        var seconds = double.Parse(text, CultureInfo.InvariantCulture);
        if (!double.IsFinite(seconds) || seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(text), "Seconds must be finite and nonnegative.");
        return TimeSpan.FromSeconds(seconds);
    }
}
