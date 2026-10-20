using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Abstractions.ECS.Queries;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.TestMod.Events;
using CitizenSimulator.TestMod.Components;

namespace CitizenSimulator.TestMod.Systems;

public sealed class MessageSystem(IEventBus events) : GameSystem
{
    private IQuery _messageComponent = null!;
    protected override void OnStart(ICommandBuffer commands)
    {
        _messageComponent = MessageComponent.GetComponentType();
    }

    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {
        //var query = Query.And(MessageComponent.GetComponentType(), Query.Or(MessageComponent.GetComponentType()));
        
        foreach (var entity in Store.Query(_messageComponent))
        {
            var message = Store.GetComponent<MessageComponent>(entity);

            if (message is null)
                continue;

            //Console.WriteLine($"[Entity {entity.Id}] {message.Message}");
            events.Publish(new MessagePublished(entity, message.Message));
            //commands.RemoveComponent<MessageComponent>(entity);
        }
    }

    protected override void OnStop(ICommandBuffer commands)
    {
        Console.WriteLine("MessageSystem stopped");
    }
}