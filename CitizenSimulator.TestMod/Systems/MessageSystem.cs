using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Abstractions.ECS.Queries;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Mod2.Events;
using CitizenSimulator.TestMod.Components;

namespace CitizenSimulator.TestMod.Systems;

public sealed class MessageSystem(IEventBus events) : GameSystem(priority: 10)
{
    private IQuery _messageComponent = null!;
    protected override void OnStart(ICommandBuffer commandBuffer)
    {
        _messageComponent = MessageComponent.GetComponentType();
    }

    protected override void OnUpdate(SimulationTime time, ICommandBuffer commandBuffer)
    {
        //var query = Query.And(MessageComponent.GetComponentType(), Query.Or(MessageComponent.GetComponentType()));
        
        foreach (var entity in Store.Query(_messageComponent))
        {
            var message = Store.GetComponent<MessageComponent>(entity);

            if (message is null)
                continue;

            //Console.WriteLine($"[Entity {entity.Id}] {message.Message}");
            events.Publish(new MessagePublished(entity, message.Message));
            //commandBuffer.RemoveComponent<MessageComponent>(entity);
        }
    }

    protected override void OnStop(ICommandBuffer commandBuffer)
    {
        Console.WriteLine("MessageSystem stopped");
    }
}