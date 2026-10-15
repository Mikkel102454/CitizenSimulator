namespace CitizenSimulator.Abstractions.ECS.Queries;

public interface IQuery
{
    bool Matches(IReadOnlyDictionary<Type, IComponent> components);
}