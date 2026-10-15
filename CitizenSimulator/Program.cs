using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Engine.ECS;
using CitizenSimulator.Engine.Modding;

namespace CitizenSimulator;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("CitizenSimulator");


        var components = new ComponentRegistry();
        var systems = new SystemRegistry();
        var store = new EntityStore();

        ComponentTypes.Initialize(components);

        var context = new ModContext(store, components, systems);
        var loader = new ModLoader(components);

        //defines a directory where we can load our mods from (from .exe path)
        string modsFolder = Path.Combine(AppContext.BaseDirectory, "Mods");
        loader.LoadMods(modsFolder, context);

        var scheduler = new SystemScheduler(systems);

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

            ulong tick = 0;
            Console.WriteLine("Start");

            while (!cancellation.IsCancellationRequested)
            {
                Console.WriteLine($"Tick {tick}");

                scheduler.Update();

                Thread.Sleep(1000 / 30);
                tick++;
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
}