namespace CitizenSimulator.Abstractions.ECS.Queries;

public sealed class AndQuery : IQuery
{
    private readonly IQuery[] _queries;

    public AndQuery(params IQuery[] queries)
    {
        ArgumentNullException.ThrowIfNull(queries);

        if (queries.Any(q => q is null))
            throw new ArgumentException("Queries cannot contain null.");

        _queries = queries.ToArray();
    }

    public bool Matches(IReadOnlyDictionary<Type, IComponent> components)
    {
        return _queries.All(query => query.Matches(components));
    }
}