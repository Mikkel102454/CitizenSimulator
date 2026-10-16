using System.Collections.Concurrent;
using CitizenSimulator.Abstractions.Timing;
namespace CitizenSimulator.Engine.Timing;
public sealed class SimulationControls(SimulationClock clock, Action quit) : ISimulationControls
{
    private readonly ConcurrentQueue<Action> _pending = new();
    public void Pause() => _pending.Enqueue(clock.Pause);
    public void Resume() => _pending.Enqueue(clock.Resume);
    public void SetSpeed(double speed) => _pending.Enqueue(() => clock.SetSpeed(speed));
    public void Step(ulong count = 1) => _pending.Enqueue(() => clock.Step(count));
    public void AdvanceBy(TimeSpan duration) => _pending.Enqueue(() => clock.AdvanceBy(duration));
    public void AdvanceTo(TimeSpan target) => _pending.Enqueue(() => clock.AdvanceTo(target));
    public void Quit() => _pending.Enqueue(quit);
    public void ApplyPending(Action<string> reportError)
    {
        for (var i = 0; i < 64 && _pending.TryDequeue(out var action); i++)
        {
            try { action(); }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
            { reportError(error.Message); }
        }
    }
}
