namespace CitizenSimulator.Abstractions.ECS;

// Used by components and to mark input as any component
public interface IComponent
{
    public EntityRef Parent { get; internal set; }
}