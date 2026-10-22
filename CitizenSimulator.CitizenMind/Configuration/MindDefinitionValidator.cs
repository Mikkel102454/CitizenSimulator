using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Planning;
using CitizenSimulator.CitizenMind.Services;

namespace CitizenSimulator.CitizenMind.Configuration;

public class MindDefinitionValidator
{
    public static void Validate(CitizenMindService service)
    {
        foreach (var action in service.Actions.Values)
        {
            RequireItems(action.Preconditions, $"Action '{action.Id}' preconditions");
            RequireItems(action.Prediction, $"Action '{action.Id}' prediction");
            RequireItems(action.Completion, $"Action '{action.Id}' completion");

            if (action.Parameters == null)
                throw new InvalidOperationException($"[CtitizenMind] Action '{action.Id}': parameters cannot be null.");

            if (action.Duration == null)
                throw new InvalidOperationException($"[CtitizenMind] Action '{action.Id}': duration is required.");

            if (action.Cost == null)
                throw new InvalidOperationException($"[CtitizenMind] Action '{action.Id}': cost is required.");

            if (action.Execution == null)
                throw new InvalidOperationException($"[CtitizenMind] Action '{action.Id}': execution cannot be null.");

            if (action.Execution.TimeoutMinutes is double timeout)
                DurationConverter.ToPositiveTimeSpan(timeout);

            if (action.CandidateProvider != null && !service.CandidateProviderFactories.ContainsKey(action.CandidateProvider))
            {
                throw new InvalidOperationException($"[CtitizenMind] Action '{action.Id}': references unknown candidate provider " + $"'{action.CandidateProvider}'.");
            }

            if(!service.Handler.ContainsKey(action.Handler))
                throw new Exception($"[CtitizenMind] Handler {action.Handler} not found.");

            for (int i = 0; i < action.Preconditions.Count; i++)
                ValidateExpression(action.Preconditions[i], $"Action '{action.Id}' preconditions[{i}]", service, action);



            ValidateExpression(action.Duration.Estimate, $"Action '{action.Id}' duration", service, action);

            ValidateExpression(action.Cost.Expression, $"Action '{action.Id}' cost", service, action, allowCalculated: true);

            for (int i = 0; i < action.Prediction.Count; i++)
                ValidateExpression(action.Prediction[i].Value, $"Action '{action.Id}' prediction[{i}].value", service, action, allowCalculated: true);


            for (int i = 0; i < action.Completion.Count; i++)
                ValidateExpression(action.Completion[i], $"Action '{action.Id}' completion[{i}]", service, action, allowBefore: true, allowCalculated: true);

            var targets = new HashSet<string>();

            foreach (var effect in action.Prediction)
            {
                PredictionOperations.ReadsTarget(effect.Op);


                if (!service.FactDescriptors.TryGetValue(effect.Fact, out var descriptor))
                {
                    throw new InvalidOperationException($"[CitizenMind] Action '{action.Id}' predicts unknown fact '{effect.Fact}'.");
                }

                if (!descriptor.CanPredict)
                {
                    throw new InvalidOperationException($"[CitizenMind] Action '{action.Id}' cannot predict changes " + $"to read-only fact '{effect.Fact}'.");
                }

                if (!targets.Add(effect.Fact))
                    throw new InvalidOperationException($"[CitizenMind] Action '{action.Id}' has multiple effects " + $"targeting '{effect.Fact}'.");
            }

            DurationConverter.GetMinutesPerUnit(action.Duration.Unit);
        }

        foreach (var goal in service.Goals.Values)
        {
            RequireItems(goal.Desired, $"Goal '{goal.Id}' desired");
            RequireItems(goal.GoalActions, $"Goal '{goal.Id}' goalActions");

            if (goal.Utility == null)
                throw new InvalidOperationException($"[CtitizenMind] Goal '{goal.Id}': utility is required.");

            RequireItems(goal.Utility.Considerations, $"Goal '{goal.Id}' considerations");

            foreach (var action in goal.GoalActions)
            {
                if(!service.Actions.ContainsKey(action))
                    throw new Exception($"[CtitizenMind] Action {action} not found.");
            }

            ValidateExpression(goal.Eligible, $"Goal '{goal.Id}' eligible", service);

            if (goal.Emergency != null)
                ValidateExpression(goal.Emergency.When, $"Goal '{goal.Id}' emergency.when", service);


            for (int i = 0; i < goal.Desired.Count; i++)
                ValidateExpression(goal.Desired[i], $"Goal '{goal.Id}' desired[{i}]", service);


            for (int i = 0; i < goal.Utility.Considerations.Count; i++)
            {
                var consideration = goal.Utility.Considerations[i];
                string location = $"Goal '{goal.Id}' considerations[{i}]";

                ValidateExpression(consideration.Input, $"{location}.input", service);
                ValidateCurve(consideration, location);
            }

            if (goal.Desired.Count == 0)
                throw new InvalidOperationException($"[CitizenMind] Goal '{goal.Id}' must have at least one desired condition.");

            if (goal.GoalActions.Count == 0)
                throw new InvalidOperationException($"[CitizenMind] Goal '{goal.Id}' must specify at least one goal action.");

            ValidateUtility(goal.Utility, $"Goal '{goal.Id}' utility");
        }
    }

    private static void ValidateExpression(ExpressionDefinition? expression, string location, CitizenMindService service,
        ActionDefinition? action = null, bool allowBefore = false, bool allowCalculated = false)
    {
        if (expression == null)
            throw new InvalidOperationException($"[CtitizenMind] {location}: expression is required.");

        if (expression.Args == null)
            throw new InvalidOperationException($"[CtitizenMind] {location}: args cannot be null.");

        if (expression.Parameter != null && (action == null || !action.Parameters.ContainsKey(expression.Parameter)))
            throw new InvalidOperationException($"[CtitizenMind] {location}: unknown parameter '{expression.Parameter}'.");


        if (expression.Before != null && !allowBefore)
            throw new InvalidOperationException($"[CtitizenMind] {location}: before-action references are unavailable here.");


        if (expression.Calculated != null)
        {
            if (!allowCalculated || expression.Calculated != "durationMinutes")
                throw new InvalidOperationException($"[CtitizenMind] {location}: calculated value " + $"'{expression.Calculated}' is unavailable here.");

        }

        int forms = 0;

        if (expression.Fact != null) forms++;
        if (expression.Literal != null) forms++;
        if (expression.Parameter != null) forms++;
        if (expression.Calculated != null) forms++;
        if (expression.Before != null) forms++;
        if (expression.Op != null) forms++;
        if (expression.Binding != null) forms++;
        if (expression.Time != null) forms++;

        if (forms != 1)
            throw new InvalidOperationException($"[CitizenMind] {location}: an expression must specify exactly one form.");

        if (expression.Binding != null)
        {
            if (string.IsNullOrWhiteSpace(expression.Binding))
                throw new InvalidOperationException($"{location}: binding name cannot be blank.");

            string? providerId = action?.CandidateProvider;

            if (providerId == null)
                throw new InvalidOperationException($"{location}: binding expressions require an action " + "with a candidate provider.");

            if (!service.CandidateProviderDescriptors.TryGetValue(providerId, out var providerDescriptor))
                throw new InvalidOperationException($"{location}: unknown candidate provider '{providerId}'.");

            if (!providerDescriptor.BindingTypes.ContainsKey(expression.Binding))
                throw new InvalidOperationException($"{location}: provider '{providerId}' does not declare " + $"binding '{expression.Binding}'.");
        }
        if (expression.Op == null && expression.Args.Count != 0)
            throw new InvalidOperationException($"[CitizenMind] {location}: only operator expressions may have arguments.");

        if (expression.Op != null)
        {
            int expected = ExpressionOperators.GetArgumentCount(expression.Op);

            if (expression.Args.Count != expected)
                throw new InvalidOperationException($"[CtitizenMind] {location}: operator '{expression.Op}' requires " + $"{expected} arguments.");
        }

        if (expression.Fact != null && !service.Facts.ContainsKey(expression.Fact))
            throw new InvalidOperationException($"[CtitizenMind] {location}: unknown fact '{expression.Fact}'.");


        if (expression.Before != null && !service.Facts.ContainsKey(expression.Before))
            throw new InvalidOperationException($"[CtitizenMind] {location}: unknown before-action fact '{expression.Before}'.");

        if (expression.Time != null && expression.Time is not ("totalMinutes" or "minuteOfDay"))
            throw new InvalidOperationException($"[CitizenMind] {location}: unknown time value " + $"'{expression.Time}'.");

        for (int i = 0; i < expression.Args.Count; i++)
            ValidateExpression(expression.Args[i], $"{location}.args[{i}]", service, action, allowBefore, allowCalculated);
    }

    private static void ValidateCurve(ConsiderationDefinition consideration, string location)
    {
        RequireItems(consideration.Curve, $"{location}.curve");

        if (consideration.Curve.Count == 0)
            throw new InvalidOperationException($"[CtitizenMind] {location}: curve must contain at least one point.");

        double previousX = double.NegativeInfinity;

        foreach (var point in consideration.Curve)
        {
            if (point == null || point.Length != 2)
                throw new InvalidOperationException($"[CtitizenMind] {location}: each curve point must contain [input, score].");

            double x = point[0];
            double y = point[1];

            if (!double.IsFinite(x) || !double.IsFinite(y))
                throw new InvalidOperationException($"[CtitizenMind] {location}: curve values must be finite.");

            if (x <= previousX)
                throw new InvalidOperationException($"[CtitizenMind] {location}: curve inputs must be strictly increasing.");

            if (y < 0 || y > 1)
                throw new InvalidOperationException($"[CtitizenMind] {location}: curve scores must be between 0 and 1.");

            previousX = x;
        }
    }

    private static void ValidateUtility(UtilityDefinition utility, string location)
    {
        if (!double.IsFinite(utility.Bias) || utility.Bias < 0)
            throw new InvalidOperationException($"[CitizenMind] {location}: bias must be finite and nonnegative.");

        double totalWeight = 0;

        for (int i = 0; i < utility.Considerations.Count; i++)
        {
            double weight = utility.Considerations[i].Weight;

            if (!double.IsFinite(weight) || weight < 0)
                throw new InvalidOperationException($"[CitizenMind] {location}: consideration[{i}] weight " + "must be finite and nonnegative.");

            totalWeight += weight;
        }

        if (!double.IsFinite(totalWeight) || totalWeight <= 0)
            throw new InvalidOperationException($"[CitizenMind] {location}: total consideration weight " + "must be finite and greater than zero.");
    }

    private static void RequireItems<T>(IEnumerable<T>? items, string location) where T : class
    {
        if (items == null)
            throw new InvalidOperationException($"[CitizenMind] {location}: collection cannot be null.");

        int index = 0;

        foreach (var item in items)
        {
            if (item == null)
                throw new InvalidOperationException($"[CitizenMind] {location}[{index}]: entry cannot be null.");

            index++;
        }
    }
}
