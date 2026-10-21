using CitizenSimulator.Abstractions.Timing;

namespace CitizenSimulator.Abstractions.ECS;

// for implmenting logic in mods
public abstract class GameSystem(int priority = 0)
{
    protected IEntityStore Store { get; private set; } = null!;
    
    // name of the system
    public virtual string Name => GetType().Name;

    // controls the execution order
    public int Priority { get; } = priority;

    public void Start(IEntityStore store, ICommandBuffer commands)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(commands);

        Store = store;
        OnStart(commands);
    }

    public void Update(SimulationTime time, ICommandBuffer commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        
        OnUpdate(time, commands);
    }

    public void Stop(ICommandBuffer commands)
    {
        ArgumentNullException.ThrowIfNull(commands);

        try
        {
            OnStop(commands);
        }
        finally
        {
            Store = null!;
        }
    }
    
    public void Remove(EntityRef entity, IComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        OnRemove(entity, component);
    }
    
    /// <summary>
    /// Called once before the first game tick.
    /// </summary>
    /// <param name="commands">The current active command buffer</param>
    protected virtual void OnStart(ICommandBuffer commands) { }
    
    /// <summary>
    /// Called every tick
    /// </summary>
    /// <param name="time">Information about the simulation time</param>
    /// <param name="commands">The current active command buffer</param>
    protected virtual void OnUpdate(SimulationTime time, ICommandBuffer commands) { }
    
    /// <summary>
    /// Called once when the engine shuts down.
    /// </summary>
    /// <param name="commands">The current active command buffer</param>
    protected virtual void OnStop(ICommandBuffer commands) { }
    
    /// <summary>
    /// Called once when a component is getting removed
    /// </summary>
    /// <param name="entity">The entity the component is getting removed from</param>
    /// <param name="component">The component getting removed</param>
    protected virtual void OnRemove(EntityRef entity, IComponent component) { }
}