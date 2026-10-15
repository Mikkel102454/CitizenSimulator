using CitizenSimulator.Abstractions.Timing;

namespace CitizenSimulator.Abstractions.ECS;

public abstract class GameSystem(int priority = 0)
{
    protected IEntityStore Store { get; private set; } = null!;
    
    // Name of the system
    public virtual string Name => GetType().Name;

    // Controls the execution order
    public int Priority { get; } = priority;

    public void Start(IEntityStore store, ICommandBuffer commandBuffer)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(commandBuffer);

        Store = store;
        OnStart(commandBuffer);
    }

    public void Update(SimulationTime time, ICommandBuffer commandBuffer)
    {
        ArgumentNullException.ThrowIfNull(commandBuffer);
        
        OnUpdate(time, commandBuffer);
    }

    public void Stop(ICommandBuffer commandBuffer)
    {
        ArgumentNullException.ThrowIfNull(commandBuffer);

        try
        {
            OnStop(commandBuffer);
        }
        finally
        {
            Store = null!;
        }
    }
    
    protected virtual void OnStart(ICommandBuffer commandBuffer) { }
    protected abstract void OnUpdate(SimulationTime time, ICommandBuffer commandBuffer);
    protected virtual void OnStop(ICommandBuffer commandBuffer) { }
}