namespace CitizenSimulator.Abstractions.ECS;

public static class ComponentTypes
{
    private static IComponentRegistry? _registry;

    public static void Initialize(IComponentRegistry registry)
    {
        _registry = registry;
    }

    public static ComponentType<T> Get<T>() where T : class, IComponent
    {
        if (_registry is null)
            throw new InvalidOperationException("Component registry has not been initialized.");

        return _registry.Get<T>();
    }
}