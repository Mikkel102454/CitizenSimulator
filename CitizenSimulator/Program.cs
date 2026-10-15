using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Engine.ECS;
using CitizenSimulator.Engine.Events;
using CitizenSimulator.Engine.Modding;
using CitizenSimulator.Engine.Timing;
using CitizenSimulator.Engine.Services;

namespace CitizenSimulator;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("CitizenSimulator");
        SimulationClock clock;
        try
        {
            clock = ParseClock(args);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Environment.ExitCode = 1;
            return;
        }

        var components = new ComponentRegistry();
        var systems = new SystemRegistry();
        var store = new EntityStore();
        var events = new EventBus();
        var services = new ServiceRegistry();
        ComponentTypes.Initialize(components);

        var context = new ModContext(store, components, systems, clock, events, services);
        var loader = new ModLoader(components);
        string modsFolder = Path.Combine(AppContext.BaseDirectory, "Mods");
        loader.LoadMods(modsFolder, context);

        var scheduler = new SystemScheduler(systems, events);
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        try
        {
            scheduler.Start(store);
            Console.WriteLine("Start. " + SimulationConsole.Help);

            var commands = new ConcurrentQueue<string>();
            // A background thread avoids blocking shutdown on a pending ReadLine.
            var input = new Thread(() => ReadCommands(commands))
            {
                IsBackground = true,
                Name = "Simulation console input"
            };
            input.Start();

            var previous = Stopwatch.GetTimestamp();
            var lastStatus = previous;
            while (!cancellation.IsCancellationRequested)
            {
                var now = Stopwatch.GetTimestamp();
                clock.AddRealTime(Stopwatch.GetElapsedTime(previous, now));
                previous = now;

                // Bound command work too, so an input flood cannot starve simulation ticks.
                for (var i = 0; i < 64 && commands.TryDequeue(out var command); i++)
                {
                    if (!SimulationConsole.Execute(command, clock, Console.Out))
                    {
                        cancellation.Cancel();
                        break;
                    }
                }

                if (cancellation.IsCancellationRequested)
                    break;
                clock.RunBatch(scheduler.Update, cancellation.Token);

                if (!Console.IsInputRedirected && Stopwatch.GetElapsedTime(lastStatus).TotalSeconds >= 1)
                {
                    Console.WriteLine(SimulationConsole.Status(clock));
                    lastStatus = Stopwatch.GetTimestamp();
                }

                // WaitOne makes even paused waits interruptible by Ctrl+C.
                var wait = clock.SuggestedWait;
                if (wait > TimeSpan.Zero)
                    cancellation.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(Math.Max(1, wait.TotalMilliseconds)));
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            Environment.ExitCode = 1;
        }
        finally
        {
            try
            {
                scheduler.Stop();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                Environment.ExitCode = 1;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
            Console.WriteLine("Stopped");
        }
    }

    private static void ReadCommands(ConcurrentQueue<string> commands)
    {
        try
        {
            while (Console.ReadLine() is { } line)
                commands.Enqueue(line);
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine("Console input stopped: " + exception.Message);
        }
    }

    private static SimulationClock ParseClock(string[] args)
    {
        var tickRate = 30;
        var batchSize = 8;
        for (var i = 0; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length ||
                !int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                throw new ArgumentException("Usage: CitizenSimulator [--tick-rate <integer>] [--batch-size <integer>]");
            switch (args[i])
            {
                case "--tick-rate": tickRate = value; break;
                case "--batch-size": batchSize = value; break;
                default: throw new ArgumentException("Unknown option: " + args[i]);
            }
        }
        return new SimulationClock(tickRate, batchSize);
    }
}
