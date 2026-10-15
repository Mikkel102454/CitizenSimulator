using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Engine.ECS;

public sealed class SystemScheduler
{
    private enum SchedulerState
    {
        Created,
        Starting,
        Running,
        Stopping,
        Stopped,
        Faulted
    }

    private SchedulerState _state = SchedulerState.Created;
    
    private readonly ISystemRegistry _registry;
    private EntityStore _store = null!;
    
    private IReadOnlyList<GameSystem> _systems = [];
    private readonly List<GameSystem> _startedSystems = new();
    
    public SystemScheduler(ISystemRegistry registry)
    {
        _registry = registry;
    }

    public void Start(EntityStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        if (_state != SchedulerState.Created)
        {
            throw new InvalidOperationException($"Cannot start the scheduler while it is {_state}.");
        }

        _store = store;
        _state = SchedulerState.Starting;

        GameSystem? startingSystem = null;

        try
        {
            _systems = _registry.GetSystems().ToArray();

            foreach (var system in _systems)
            {
                startingSystem = system;

                ExecuteCallback(buffer => system.Start(_store, buffer));

                _startedSystems.Add(system);
                startingSystem = null;
            }

            _state = SchedulerState.Running;
        }
        catch (Exception startupException)
        {
            _state = SchedulerState.Faulted;

            var errors = new List<Exception>
            {
                new InvalidOperationException($"Startup failed in system '{startingSystem?.Name ?? "registry snapshot"}'.", startupException)
            };

            if (startingSystem is not null)
            {
                var failedSystem = startingSystem;

                try
                {
                    ExecuteCallback(buffer => failedSystem.Stop(buffer));
                }
                catch (Exception cleanupException)
                {
                    errors.Add(new InvalidOperationException($"System '{failedSystem.Name}' failed during startup cleanup.", cleanupException));
                }
            }

            try
            {
                Stop();
            }
            catch (Exception shutdownException)
            {
                errors.Add(shutdownException);
            }

            throw new AggregateException("Scheduler startup failed.", errors);
        }
    }
    public void Update()
    {
        if (_state != SchedulerState.Running)
        {
            throw new InvalidOperationException($"Cannot update the scheduler while it is {_state}.");
        }

        foreach (var system in _systems)
        {
            try
            {
                ExecuteCallback(buffer => system.Update(buffer));
            }
            catch (Exception exception)
            {
                _state = SchedulerState.Faulted;

                throw new InvalidOperationException($"System '{system.Name}' failed during update.", exception);
            }
        }
    }

    public void Stop()
    {
        if (_state == SchedulerState.Stopped)
            return;

        if (_state == SchedulerState.Created)
        {
            _state = SchedulerState.Stopped;
            return;
        }

        if (_state != SchedulerState.Running && _state != SchedulerState.Faulted)
        {
            throw new InvalidOperationException($"Cannot stop the scheduler while it is {_state}.");
        }

        _state = SchedulerState.Stopping;
        var errors = new List<Exception>();

        try
        {
            //reversed to shutdown correctly
            foreach (var system in _startedSystems.AsEnumerable().Reverse())
            {
                try
                {
                    ExecuteCallback(buffer => system.Stop(buffer));
                }
                catch (Exception exception)
                {
                    errors.Add(new InvalidOperationException($"System '{system.Name}' failed during shutdown.", exception));
                }
            }
        }
        finally
        {
            _startedSystems.Clear();
            _state = SchedulerState.Stopped;
        }

        if (errors.Count > 0)
        {
            throw new AggregateException("One or more systems failed during shutdown.", errors);
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
            catch (Exception exception)
            {
                throw new InvalidOperationException("System callback failed.", exception);
            }
            finally
            {
                _store.EndSystemExecution();
            }

            try
            {
                commandBuffer.Playback();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("Command buffer playback failed.", exception);
            }
        }
        finally
        {
            commandBuffer.Discard();
        }
    }
}