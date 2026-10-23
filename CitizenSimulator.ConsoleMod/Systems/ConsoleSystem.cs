using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Input;
using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.ConsoleMod.Services;

namespace CitizenSimulator.ConsoleMod.Systems;

internal sealed class ConsoleSystem(IModContext context) : GameSystem
{
    private IDisposable? _line;
    private IDisposable? _error;
    protected override void OnStart(ICommandBuffer commands)
    {
        Console.WriteLine(ConsoleService.Help);
        _line = context.InputEvents.Subscribe<LineSubmitted>((line, time, buffer) =>
            ConsoleService.Execute(line.Text, context.Clock, context.Controls, Console.Out, context.Store, context.Services, context.Systems));
        _error = context.InputEvents.Subscribe<ControlFailed>((error, time, buffer) =>
            Console.WriteLine("Control error: " + error.Message));
    }

    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {

    }
    protected override void OnStop(ICommandBuffer commands) { _line?.Dispose(); _error?.Dispose(); }
}
