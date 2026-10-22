using System.Collections.ObjectModel;
using System.Globalization;
using CitizenSimulator.CitizenMind.Definitions;

namespace CitizenSimulator.CitizenMind.Configuration;

internal static class ParameterParser
{
    public static object Parse(ParameterDefinition definition, string location)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.Value))
            throw new InvalidOperationException($"{location}: parameter value is required.");

        string text = definition.Value;

        switch (definition.Type)
        {
            case ParameterType.BOOLEAN:
                if (bool.TryParse(text, out bool boolean))
                    return boolean;
                break;

            case ParameterType.INTEGER:
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer))
                    return integer;
                break;

            case ParameterType.DOUBLE:
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture,
                        out double number) && double.IsFinite(number))
                    return number;
                break;

            case ParameterType.DECIMAL:
                if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal decimalNumber))
                    return decimalNumber;
                break;

            default:
                throw new InvalidOperationException($"{location}: unsupported parameter type '{definition.Type}'.");
        }

        throw new InvalidOperationException($"{location}: '{text}' is not a valid {definition.Type} value.");
    }

    public static IReadOnlyDictionary<string, object?> ParseAll(IReadOnlyDictionary<string, ParameterDefinition> definitions, string actionId)
    {
        if (definitions == null)
            throw new InvalidOperationException($"Action '{actionId}': parameters cannot be null.");

        var values = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var (name, definition) in definitions)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException($"Action '{actionId}': parameter name cannot be blank.");

            values.Add(name, Parse(definition, $"Action '{actionId}' parameter '{name}'"));
        }

        return new ReadOnlyDictionary<string, object?>(values);
    }
}
