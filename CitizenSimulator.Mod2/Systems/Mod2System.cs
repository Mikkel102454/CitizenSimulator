using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Mod2.Events;

namespace CitizenSimulator.Mod2.Systems;

public sealed class Mod2System(IEventBus events) : GameSystem(priority: 10)
{
    private IDisposable? _subscription;
    protected override void OnStart(ICommandBuffer commandBuffer)
    {
        _subscription = events.Subscribe<MessagePublished>(OnMessage);
    }

    private void OnMessage(MessagePublished eventData, SimulationTime time, ICommandBuffer commandBuffer)
    {
        Console.WriteLine(
            $"[Entity {eventData.Entity.Id}] {eventData.Message}");
    }

    protected override void OnUpdate(SimulationTime time, ICommandBuffer commandBuffer)
    {
    }

    protected override void OnStop(ICommandBuffer commandBuffer)
    {
        _subscription?.Dispose();
        _subscription = null;
    }
}