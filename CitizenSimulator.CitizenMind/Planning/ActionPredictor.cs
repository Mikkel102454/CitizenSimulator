using CitizenSimulator.CitizenMind.Definitions;
using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Models;

namespace CitizenSimulator.CitizenMind.Planning;

public class ActionPredictor
{
    
    public static EvaluationContext Predict(ActionDefinition action, EvaluationContext state, double durationMinutes,
        IReadOnlyDictionary<string, FactDescriptor> descriptors)
    {
        return Predict(
            new ActionCandidate
            {
                Id = action.Id,
                Definition = action
            }, 
            state, durationMinutes, descriptors);
    }
    
    public static EvaluationContext Predict(ActionCandidate candidate, EvaluationContext state, double durationMinutes,
        IReadOnlyDictionary<string, FactDescriptor> descriptors)
    {
        var action = candidate.Definition;
        
        // new copy
        Dictionary<string, object?> facts = new(state.Facts, state.Facts.Comparer);
        
        var context = new EvaluationContext
        {
            Facts = state.Facts,
            Parameters = action.ResolvedParameters,
            Bindings = candidate.Bindings,
            GameTime = state.GameTime,
            Calculated = new Dictionary<string, double>
            {
                ["durationMinutes"] = durationMinutes
            }
        };
        
        var targets = new HashSet<string>(facts.Comparer);

        foreach (var effect in action.Prediction)
        {
            if (!descriptors.TryGetValue(effect.Fact, out var descriptor))
                throw new InvalidOperationException($"Unknown fact descriptor '{effect.Fact}'.");

            if (!descriptor.CanPredict)
                throw new InvalidOperationException($"Cannot predict changes to read-only fact '{effect.Fact}'.");
            
            if (!facts.ContainsKey(effect.Fact))
                throw new InvalidOperationException($"Unknown fact '{effect.Fact}'.");

            if (!targets.Add(effect.Fact))
                throw new InvalidOperationException($"Multiple effects target fact '{effect.Fact}'.");

            var value = ExpressionEvaluator.Resolve(context, effect.Value);

            var original = state.Facts[effect.Fact];

            switch (effect.Op)
            {
                case "set":
                    facts[effect.Fact] = RequireValue(value, descriptor);
                    break;

                case "add":
                case "subtract":
                {
                    if (descriptor.ValueType == typeof(int))
                    {
                        int currentInt = NumericValues.RequireInteger(original, effect.Fact);
                        int amountInt = NumericValues.RequireInteger(value, effect.Fact);

                        int resultInt = effect.Op == "add" ? checked(currentInt + amountInt) : checked(currentInt - amountInt);

                        facts[effect.Fact] = resultInt;
                        break;
                    }
                    
                    if (descriptor.ValueType == typeof(decimal))
                    {
                        decimal currentDecimal = NumericValues.RequireDecimal(original, effect.Fact);
                        decimal amountDecimal = NumericValues.RequireDecimal(value, effect.Fact);

                        decimal resultDecimal = effect.Op == "add" ? currentDecimal + amountDecimal : currentDecimal - amountDecimal;

                        facts[effect.Fact] = resultDecimal;
                        break;
                    }
                    
                    if (descriptor.ValueType != typeof(double))
                        throw new InvalidOperationException($"[CitizenMind] Arithmetic prediction requires an int, double or decimal fact: " + $"'{effect.Fact}'.");
                    
                    double current = NumericValues.RequireNumber(original, effect.Fact);
                    double amount = NumericValues.RequireNumber(value, effect.Fact);

                    double result = effect.Op == "add" ? current + amount : current - amount;

                    if (!double.IsFinite(result))
                        throw new InvalidOperationException($"[CitizenMind] Prediction for '{effect.Fact}' produced a nonfinite result.");

                    facts[effect.Fact] = result;
                    break;
                }

                default:
                    throw new InvalidOperationException($"[CitizenMind] Unsupported prediction operation '{effect.Op}'. Valid operations are 'set', 'add', 'subtract'.");
            }
        }

        return new EvaluationContext
        {
            Facts = facts,
            GameTime = state.GameTime + TimeSpan.FromMinutes(durationMinutes)
        };
    }
    
    private static object RequireValue(object? value, FactDescriptor descriptor)
    {
        if (descriptor.ValueType == typeof(bool))
        {
            if (value is bool boolean)
                return boolean;

            throw new InvalidOperationException($"[CitizenMind] Fact '{descriptor.Id}' requires a boolean.");
        }

        if (descriptor.ValueType == typeof(double))
            return NumericValues.RequireNumber(value, descriptor.Id);
        
        if (descriptor.ValueType == typeof(int))
            return NumericValues.RequireInteger(value, descriptor.Id);
        
        if (descriptor.ValueType == typeof(decimal))
            return NumericValues.RequireDecimal(value, descriptor.Id);
        
        throw new InvalidOperationException($"[CitizenMind] Prediction type '{descriptor.ValueType.Name}' " + $"is not supported for fact '{descriptor.Id}'.");
    }
}