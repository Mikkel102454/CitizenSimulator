using CitizenSimulator.Abstractions.ECS.Queries;

namespace CitizenSimulator.Abstractions.ECS;

public readonly record struct ComponentType<T>(int Id) : IQuery where T : class, IComponent
{
    public bool Matches( IReadOnlyDictionary<Type, IComponent> components)
    {
        return components.ContainsKey(typeof(T));
    }
}