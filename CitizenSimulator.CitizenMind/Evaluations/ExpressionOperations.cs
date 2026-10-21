namespace CitizenSimulator.CitizenMind.Evaluations;

internal static class ExpressionOperators
{
    public static int GetArgumentCount(string? op)
    {
        return op switch
        {
            "add" or "subtract" or "multiply" or "divide"
                or "min" or "max"
                or "gt" or "gte" or "lt" or "lte" => 2,

            _ => throw new InvalidOperationException(
                $"Unsupported operator '{op}'.")
        };
    }
}