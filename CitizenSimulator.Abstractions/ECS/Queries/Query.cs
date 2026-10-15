namespace CitizenSimulator.Abstractions.ECS.Queries;

public static class Query
{
    public static IQuery And(params IQuery[] queries)
    {
        return new AndQuery(queries);
    }

    public static IQuery Or(params IQuery[] queries)
    {
        return new OrQuery(queries);
    }

    public static IQuery Not(IQuery query)
    {
        return new NotQuery(query);
    }
}