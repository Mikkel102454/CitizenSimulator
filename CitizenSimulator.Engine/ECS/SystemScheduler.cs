using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Engine.ECS;

public sealed class SystemScheduler
{
    private readonly ISystemRegistry _registry;

    public SystemScheduler(ISystemRegistry registry)
    {
        _registry = registry;
    }

    public void Start(IEntityStore store)
    {
        foreach (var system in _registry.GetSystems())
        {
            system.Start(store);
        }
    }

    public void Update()
    {
        foreach (var system in _registry.GetSystems())
        {
            system.Update();
        }
    }

    public void Stop()
    {
        foreach (var system in _registry.GetSystems().Reverse())
        {
            system.Stop();
        }
    }
}