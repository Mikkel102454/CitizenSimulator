namespace CitizenSimulator.Abstractions.ECS;

public abstract class Component<T> : IComponent
    where T : Component<T>
{
    public static ComponentType<T> GetComponentType()
    {
        return ComponentTypes.Get<T>();
    }
}