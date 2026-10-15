namespace CitizenSimulator.Abstractions.ECS.Queries;

public sealed class OrQuery : IQuery
{
    private readonly IQuery[] _queries;

    public OrQuery(params IQuery[] queries)
    {
        ArgumentNullException.ThrowIfNull(queries);

        if (queries.Any(q => q is null))
            throw new ArgumentException("Queries cannot contain null.");

        _queries = queries.ToArray();
    }

    public bool Matches(IReadOnlyDictionary<Type, IComponent> components)
    {
        return _queries.Any(query => query.Matches(components));
    }
}