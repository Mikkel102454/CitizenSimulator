namespace CitizenSimulator.Abstractions.ECS;

public interface IComponentRegistry
{
    // Register a new component type
    ComponentType<T> Register<T>() where T : class, IComponent;

    // Get an already registered component type
    ComponentType<T> Get<T>() where T : class, IComponent;

    // Check if a component type is registered
    bool IsRegistered<T>() where T : class, IComponent;
}