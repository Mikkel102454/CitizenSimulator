using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.ECS.Queries;
using CitizenSimulator.TestMod.Components;

namespace CitizenSimulator.TestMod.Systems;

public sealed class MessageSystem() : GameSystem(priority: 10)
{
    private IQuery _messageComponent = null!;
    protected override void OnStart()
    {
        _messageComponent = MessageComponent.GetComponentType();
    }

    protected override void OnUpdate()
    {
        //var query = Query.And(MessageComponent.GetComponentType());
        
        foreach (var entity in Store.Query(_messageComponent))
        {
            var message = Store.GetComponent<MessageComponent>(entity);

            if (message is null)
                continue;

            Console.WriteLine($"[Entity {entity.Id}] {message.Message}");
        }
    }
}