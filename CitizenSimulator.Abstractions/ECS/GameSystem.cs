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
    
    protected virtual void OnStart(ICommandBuffer commands) { }
    protected virtual void OnUpdate(SimulationTime time, ICommandBuffer commands) { }
    protected virtual void OnStop(ICommandBuffer commands) { }
}