namespace CitizenSimulator.Abstractions.ECS;

public interface IComponentRegistry
{
    // register a new component type
    ComponentType<T> Register<T>() where T : class, IComponent;

    //gGet an already registered component type
    ComponentType<T> Get<T>() where T : class, IComponent;

    // check if a component type is registered
    bool IsRegistered<T>() where T : class, IComponent;
}