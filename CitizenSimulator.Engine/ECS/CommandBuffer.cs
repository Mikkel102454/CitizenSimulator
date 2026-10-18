using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Engine.ECS;

public sealed class CommandBuffer(EntityStore store) : ICommandBuffer
{
    private readonly List<ICommand> _commands = new(); // commands that needs to be executed this tick
    private readonly HashSet<EntityRef> _pendingEntities = new(); // entities that is pending its creation
    private readonly HashSet<IComponent> _pendingComponents = new(ReferenceEqualityComparer.Instance);
    private bool _isClosed;
    
    private interface ICommand // basic command interface
    {
        void Execute(EntityStore store);
    }
    
    // command definitions are in two parts.
    // first we have the command class which contains instruction
    // of how to execute and needed data. next we have the public function witch
    // is what everything else calls to execute the command
    private sealed class CreateEntityCommand(EntityRef entity) : ICommand 
    {
        public EntityRef Entity { get; } = entity;
        public void Execute(EntityStore store)
        {
            store.ActivateEntity(Entity);
        }
    }
    public EntityRef CreateEntity()
    {
        EnsureOpen(); // ensure we havent already operated the command buffer this tick
        EntityRef entity = store.ReserveEntity(); // reserve and entities id and get a ref back
        _pendingEntities.Add(entity);
        _commands.Add(new CreateEntityCommand(entity));
        return entity;
    }
    
    private sealed class DestroyEntityCommand(EntityRef entity) : ICommand
    {
        public void Execute(EntityStore store)
        {
            store.DestroyEntity(entity);
        }
    }
    public void DestroyEntity(EntityRef entity)
    {
        EnsureOpen();
        ValidateReference(entity);
        _commands.Add(new DestroyEntityCommand(entity));
    }
    
    private sealed class TryDestroyEntityCommand(EntityRef entity) : ICommand
    {
        public void Execute(EntityStore store)
        {
            if (store.IsAlive(entity))
            {
                store.DestroyEntity(entity);
            }
        }
    }
    public void TryDestroyEntity(EntityRef entity)
    {
        EnsureOpen();
        ValidateReference(entity);
        _commands.Add(new TryDestroyEntityCommand(entity));
    }
    
    private sealed class AddComponentCommand<T>(EntityRef entity, T component) : ICommand where T : class, IComponent
    {
        public void Execute(EntityStore store)
        {
            store.AddComponent(entity, component);
        }
    }
    public T AddComponent<T>(EntityRef entity, T component) where T : class, IComponent
    {
        EnsureOpen();
        ValidateReference(entity);
        ArgumentNullException.ThrowIfNull(component);

        EntityStore.AssignParent(entity, component);
        _pendingComponents.Add(component);
        _commands.Add(new AddComponentCommand<T>(entity, component));

        return component;
    }
    
    private sealed class RemoveComponentCommand<T>(EntityRef entity) : ICommand where T : class, IComponent
    {
        public void Execute(EntityStore store)
        {
            store.RemoveComponent<T>(entity);
        }
    }
    public void RemoveComponent<T>(EntityRef entity) where T : class, IComponent
    {
        EnsureOpen();
        ValidateReference(entity);
        _commands.Add(new RemoveComponentCommand<T>(entity));
    }

    // throw if we have already operated the buffer this tick
    private void EnsureOpen()
    {
        if (_isClosed)
        {
            throw new InvalidOperationException(
                "This command buffer has already been played back or discarded.");
        }
    }
    
    // operate the buffer this tick
    internal void Playback()
    {
        EnsureOpen();
        try
        {
            for (int index = 0; index < _commands.Count; index++) // loop all command
            {
                var command = _commands[index];

                try
                {
                    command.Execute(store);
                }
                catch (Exception exception) // catch exceptions and say what command it was
                {
                    throw new InvalidOperationException($"Command #{index + 1} ({command.GetType().Name}) failed.", exception);
                }

                if (command is CreateEntityCommand creation)
                {
                    _pendingEntities.Remove(creation.Entity);
                }
            }
        }
        finally
        {
            Discard();
        }
    }
    
    // discard the buffer (this marks the buffer as have been operated this tick)
    internal void Discard()
    {
        if (_isClosed)
            return;

        foreach (var component in _pendingComponents)
        {
            if (!store.ContainsComponent(component))
            {
                component.Parent = default;
            }
        }

        foreach (var entity in _pendingEntities)
        {
            store.CancelReservation(entity);
        }

        // clear data
        _pendingEntities.Clear();
        _pendingComponents.Clear();
        _commands.Clear();
        _isClosed = true;
    }
    
    private void ValidateReference(EntityRef entity) // make sure we own the reference and not someone else
    {
        if (!store.OwnsReference(entity))
        {
            throw new InvalidOperationException(
                "Entity reference belongs to another store or has an invalid generation.");
        }
        if (store.IsReserved(entity) && !_pendingEntities.Contains(entity))
        {
            throw new InvalidOperationException(
                "Entity is reserved by another command buffer.");
        }
    }
}
