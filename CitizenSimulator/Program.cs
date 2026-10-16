using System.Diagnostics;
using System.Globalization;
using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Input;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Engine.ECS;
using CitizenSimulator.Engine.Events;
using CitizenSimulator.Engine.Input;
using CitizenSimulator.Engine.Modding;
using CitizenSimulator.Engine.Timing;
using CitizenSimulator.Engine.Services;

namespace CitizenSimulator;

class Program
{
    static void Main(string[] args)
    {
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
        var inputState = new InputState();
        var inputEvents = new EventBus();
        var inputDispatcher = new InputDispatcher(inputState, inputEvents);
        using var cancellation = new CancellationTokenSource();
        var controls = new SimulationControls(clock, cancellation.Cancel);
        ComponentTypes.Initialize(components);

        services.Register<IInputState>(inputState);

        var context = new ModContext(store, components, systems, clock, events, services, inputEvents, controls);
        var loader = new ModLoader(components);

        string modsFolder = Path.Combine(AppContext.BaseDirectory, "Mods");
        loader.LoadMods(modsFolder, context);

        var scheduler = new SystemScheduler(systems, events);
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        try
        {
            scheduler.Start(store);

            // we use a background thread to avoid blocking program on shutdown
            // also using for disposing the console after we loose scope so we 
            // can restore the console
            using var nativeInput = WindowsConsoleInput.TryCreate(inputDispatcher);
            var input = new Thread(() => ReadLines(inputDispatcher))
            {
                IsBackground = true,
                Name = "Simulation console input"
            };
            
            if (nativeInput is null) input.Start();

            var previous = Stopwatch.GetTimestamp();

            while (!cancellation.IsCancellationRequested)
            {
                var now = Stopwatch.GetTimestamp();
                clock.AddRealTime(Stopwatch.GetElapsedTime(previous, now));
                previous = now;

                nativeInput?.Poll();
                inputDispatcher.Drain();
                scheduler.DispatchInput(inputEvents, new SimulationTime(clock.Tick, TimeSpan.Zero, clock.Elapsed));
                void ApplyControls() => controls.ApplyPending(message => inputEvents.Publish(new ControlFailed(message)));
                ApplyControls();
                if (cancellation.IsCancellationRequested) break;
                clock.RunBatch(time =>
                {
                    inputDispatcher.PublishHeldEvents();
                    scheduler.DispatchInput(inputEvents, time);
                    scheduler.Update(time);
                }, cancellation.Token, ApplyControls);

                // WaitOne makes even paused waits interruptible with ctrl c
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

    private static void ReadLines(InputDispatcher input)
    {
        try
        {
            while (Console.ReadLine() is { } line)
                input.SubmitLine(line);
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
            if (i + 1 >= args.Length || !int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
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
