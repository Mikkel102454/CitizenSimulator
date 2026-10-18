namespace CitizenSimulator.Abstractions.ECS;

public abstract class Component<T> : IComponent where T : Component<T>
{
    // Assigned when attached or queued for attachment; default when detached.
    public EntityRef Parent { get; internal set; }

    EntityRef IComponent.Parent
    {
        get => Parent;
        set => Parent = value;
    }

    public static ComponentType<T> GetComponentType()
    {
        return ComponentTypes.Get<T>();
    }
}
