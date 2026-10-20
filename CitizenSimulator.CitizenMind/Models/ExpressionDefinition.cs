using CitizenSimulator.CitizenMind.Components;
using CitizenSimulator.CitizenMind.Services;

namespace CitizenSimulator.CitizenMind.Models;

public sealed class ExpressionDefinition
{
    public string? Fact { get; init; }

    public string? Binding { get; init; }

    public object? Literal { get; init; }

    public string? Op { get; init; }

    public List<ExpressionDefinition> Args { get; init; } = [];

    public bool Evaluate(CitizenMindService service, MindComponent mind)
    {

        object? value = Resolve(service, mind);

        if (value is bool b)
            return b;

        if (value is null)
            return false;

        throw new InvalidOperationException("Expected a boolean expression.");
    }
    
    public object? Resolve(CitizenMindService service, MindComponent mind)
    {
        if (Fact is not null)
        {
            if (!service.Facts.TryGetValue(Fact, out var provider))
                throw new InvalidOperationException($"Unknown fact '{Fact}'.");

            return provider.DynamicInvoke(mind.Parent);
        }

        if (Literal is not null)
            return Literal;

        if (Op is not (
            "add" or "subtract" or "multiply" or "divide" or
            "gt" or "gte" or "lt" or "lte"))
            throw new InvalidOperationException(
                $"Unsupported operator '{Op}'.");

        if (Args.Count != 2)
            throw new InvalidOperationException(
                $"Operator '{Op}' requires two arguments.");

        // resolve recursively so arguments can contain math.
        var left = Args[0].Resolve(service, mind);
        var right = Args[1].Resolve(service, mind);

        if (left is null || right is null)
            return null;

        double a = ToNumber(left);
        double b = ToNumber(right);

        object result = Op switch
        {
            "add"      => a + b,
            "subtract" => a - b,
            "multiply" => a * b,
            "divide"   => b != 0 ? a / b : throw new DivideByZeroException(),

            "gt"  => a > b,
            "gte" => a >= b,
            "lt"  => a < b,
            "lte" => a <= b,

            _ => throw new InvalidOperationException()
        };

        if (result is double number && !double.IsFinite(number))
            throw new InvalidOperationException("Nonfinite math result.");

        return result;
    }
    
    private static double ToNumber(object value)
    {
        if (value is bool)
            throw new InvalidOperationException("Expected a number.");

        double number = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);

        if (!double.IsFinite(number))
            throw new InvalidOperationException("Expected a finite number.");

        return number;
    }
}