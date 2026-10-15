using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Engine.ECS;

public sealed class SystemScheduler
{
    private readonly ISystemRegistry _registry;
    private EntityStore _store = null!;
    
    public SystemScheduler(ISystemRegistry registry)
    {
        _registry = registry;
    }

    public void Start(EntityStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        foreach (var system in _registry.GetSystems())
        {
            ExecuteCallback(buffer => system.Start(_store, buffer));
        }
    }

    public void Update()
    {
        if (_store is null)
        {
            throw new InvalidOperationException(
                "The scheduler must be started before updating.");
        }

        foreach (var system in _registry.GetSystems())
        {
            ExecuteCallback(buffer => system.Update(buffer));
        }
    }

    public void Stop()
    {
        //reversed here to shutdown correctly
        foreach (var system in _registry.GetSystems().Reverse())
        {
            ExecuteCallback(buffer => system.Stop(buffer));
        }
    }
    
    private void ExecuteCallback(Action<ICommandBuffer> callback)
    {
        var commandBuffer = new CommandBuffer(_store);

        try
        {
            _store.BeginSystemExecution();

            try
            {
                callback(commandBuffer);
            }
            finally
            {
                _store.EndSystemExecution();
            }

            commandBuffer.Playback();
        }
        finally
        {
            commandBuffer.Discard();
        }
    }
}