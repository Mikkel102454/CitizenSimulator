using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Engine.ECS;

public sealed class CommandBuffer(EntityStore store) : ICommandBuffer
{
    private readonly List<ICommand> _commands = new();
    private readonly HashSet<EntityRef> _pendingEntities = new();
    private bool _isClosed;
    
    private interface ICommand
    {
        void Execute(EntityStore store);
    }
    
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
        EnsureOpen();
        EntityRef entity = store.ReserveEntity();
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

    private void EnsureOpen()
    {
        if (_isClosed)
        {
            throw new InvalidOperationException(
                "This command buffer has already been played back or discarded.");
        }
    }
    
    internal void Playback()
    {
        EnsureOpen();
        try
        {
            for (int index = 0; index < _commands.Count; index++)
            {
                var command = _commands[index];

                try
                {
                    command.Execute(store);
                }
                catch (Exception exception)
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
    
    internal void Discard()
    {
        if (_isClosed)
            return;

        foreach (var entity in _pendingEntities)
        {
            store.CancelReservation(entity);
        }

        _pendingEntities.Clear();
        _commands.Clear();
        _isClosed = true;
    }
    
    private void ValidateReference(EntityRef entity)
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