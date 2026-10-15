namespace CitizenSimulator.Abstractions.ECS;

public abstract class GameSystem(int priority = 0)
{
    protected IEntityStore Store { get; private set; } = null!;
    
    // Name of the system
    public virtual string Name => GetType().Name;

    // Controls the execution order
    public int Priority { get; } = priority;

    public void Start(IEntityStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Store = store;

        OnStart();
    }

    public void Update()
    {
        // Required engine logic before each update goes here.
        OnUpdate();
    }

    public void Stop()
    {
        try
        {
            OnStop();
        }
        finally
        {
            // Required engine cleanup goes here.
            Store = null!;
        }
    }
    
    protected virtual void OnStart() { }
    protected abstract void OnUpdate();
    protected virtual void OnStop() { }
}