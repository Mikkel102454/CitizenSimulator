using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Engine.Events;

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

    private SchedulerState _state = SchedulerState.Created; // the state to track what can happen
    
    private readonly ISystemRegistry _registry;
    private EntityStore _store = null!;
    private readonly EventBus _events;
    
    private IReadOnlyList<GameSystem> _systems = [];
    private readonly List<GameSystem> _startedSystems = new();
    
    public SystemScheduler(ISystemRegistry registry, EventBus events)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(events);
        
        _registry = registry;
        _events = events;
    }

    public void Start(EntityStore store) // start the schedular to tick
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

                // run start in all systems
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
                    // run stop in all systems if we failed to start all mods
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
    public void Update(SimulationTime time)
    {
        if (_state != SchedulerState.Running)
        {
            throw new InvalidOperationException($"Cannot update the scheduler while it is {_state}.");
        }

        foreach (var system in _systems)
        {
            try
            {
                ExecuteCallback(buffer => system.Update(time, buffer));
            }
            catch (Exception exception)
            {
                _state = SchedulerState.Faulted;

                throw new InvalidOperationException($"System '{system.Name}' failed during update.", exception);
            }
        }
        
        // send out event after we updated (so at end of tick)
        try
        {
            _events.Dispatch(time, ExecuteCallback);
        }
        catch (Exception exception)
        {
            _state = SchedulerState.Faulted;

            throw new InvalidOperationException(
                "Event delivery failed during update.", exception);
        }
    }

    // Dispatches independently of simulation ticks, including while paused.
    public void DispatchInput(EventBus inputEvents, SimulationTime time)
    {
        if (_state != SchedulerState.Running)
            throw new InvalidOperationException($"Cannot dispatch input while scheduler is {_state}.");
        try { inputEvents.Dispatch(time, ExecuteCallback); }
        catch { _state = SchedulerState.Faulted; throw; }
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
    
    
    // executes the parsed in function and lock the entity store cause the schedular now runs
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

            // after this singular mods runs its tick all its commands get played
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