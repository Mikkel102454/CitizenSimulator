using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.CitizenMind.Components;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Services;

namespace CitizenSimulator.CitizenMind.Evaluations;

public class ExpressionEvaluator
{
    /// <summary>
    /// Returns weather an expression is true or false
    /// </summary>
    /// <param name="context"></param>
    /// <param name="definition">The ExpressionDefinition to resolve</param>
    /// <returns>Expression evaluation</returns>
    /// <exception cref="InvalidOperationException">Expression didnt return a boolean</exception>
    public static bool Evaluate(EvaluationContext context, ExpressionDefinition definition)
    {

        object? value = Resolve(context, definition);

        if (value is bool b)
            return b;

        if (value is null)
            return false;

        throw new InvalidOperationException("Expected a boolean expression.");
    }

    /// <summary>
    /// Resolves for the value of a given expression
    /// </summary>
    /// <param name="context"></param>
    /// <param name="definition">The ExpressionDefinition to resolve</param>
    /// <returns>Resolved value</returns>
    /// <exception cref="InvalidOperationException">The operation keyword was unknown</exception>
    /// <exception cref="DivideByZeroException">There was division by 0 in math equation</exception>
    public static object? Resolve(EvaluationContext context, ExpressionDefinition definition)
    {
        if (definition.Fact != null)
        {
            if (!context.Facts.TryGetValue(definition.Fact, out var value))
                throw new InvalidOperationException($"Unknown fact '{definition.Fact}'.");

            return value;
        }

        if (definition.Parameter != null)
        {
            if (!context.Parameters.TryGetValue(definition.Parameter, out var value))
                throw new InvalidOperationException($"Unknown parameter '{definition.Parameter}'.");

            return value;
        }

        if (definition.Calculated != null)
        {
            if (!context.Calculated.TryGetValue(definition.Calculated, out var value))
                throw new InvalidOperationException($"Unknown calculated value '{definition.Calculated}'.");

            return value;
        }

        if (definition.Literal != null)
            return definition.Literal;

        if (definition.Before != null)
        {
            if (!context.Before.TryGetValue(definition.Before, out var value))
                throw new InvalidOperationException($"Missing before action fact '{definition.Before}'.");

            return value;
        }
        
        int expected = ExpressionOperators.GetArgumentCount(definition.Op);

        if (definition.Args.Count != expected)
            throw new InvalidOperationException(
                $"Operator '{definition.Op}' requires {expected} arguments.");

        // resolve recursively so arguments can contain math.
        var left = Resolve(context, definition.Args[0]);
        var right = Resolve(context, definition.Args[1]);

        if (left == null || right == null)
            return null;

        double a = ToNumber(left);
        double b = ToNumber(right);

        object result = definition.Op switch
        {
            "add"      => a + b,
            "subtract" => a - b,
            "multiply" => a * b,
            "divide"   => b != 0 ? a / b : throw new DivideByZeroException(),
            "min"   => Math.Min(a, b),
            "max"   => Math.Max(a, b),

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