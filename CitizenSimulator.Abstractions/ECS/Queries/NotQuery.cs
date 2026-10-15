namespace CitizenSimulator.Abstractions.ECS.Queries;

public sealed class NotQuery : IQuery
{
    private readonly IQuery _query;

    public NotQuery(IQuery query)
    {
        _query = query ??
                 throw new ArgumentNullException(nameof(query));
    }

    public bool Matches(IReadOnlyDictionary<Type, IComponent> components)
    {
        return !_query.Matches(components);
    }
}