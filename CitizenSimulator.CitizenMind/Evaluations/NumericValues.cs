namespace CitizenSimulator.CitizenMind.Evaluations;

internal static class NumericValues
{
    public static double RequireNumber(object? value, string fact)
    {
        if (value is not (int or float or double or decimal))
            throw new InvalidOperationException($"[CitizenMind] Prediction for '{fact}' requires a number.");

        double number = Convert.ToDouble(value);

        if (!double.IsFinite(number))
            throw new InvalidOperationException($"[CitizenMind] Prediction for '{fact}' requires a finite number.");

        return number;
    }
    
    public static int RequireInteger(object? value, string fact)
    {
        if (value is decimal decimalValue)
        {
            if (decimal.Truncate(decimalValue) != decimalValue ||
                decimalValue < int.MinValue ||
                decimalValue > int.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Fact '{fact}' requires a whole number within Int32 range.");
            }

            return (int)decimalValue;
        }

        double number = RequireNumber(value, fact);

        if (Math.Truncate(number) != number ||
            number < int.MinValue ||
            number > int.MaxValue)
        {
            throw new InvalidOperationException(
                $"Fact '{fact}' requires a whole number within Int32 range.");
        }

        return checked((int)number);
    }
    
    public static decimal RequireDecimal(object? value, string fact)
    {
        return value switch
        {
            decimal decimalValue => decimalValue,
            int integerValue => integerValue,

            _ => throw new InvalidOperationException($"Fact '{fact}' requires a decimal or integer value.")
        };
    }
}