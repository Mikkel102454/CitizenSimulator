using System.Globalization;
using CitizenSimulator.CitizenMind.Configuration;
using CitizenSimulator.CitizenMind.Definitions;
using CitizenSimulator.CitizenMind.Models;

namespace CitizenSimulator.CitizenMind.Planning;

internal static class WaitCandidateFactory
{
    public static ActionCandidate Create(TimeSpan until)
    {
        var target = new ExpressionDefinition
        {
            Parameter = "untilMinutes"
        };

        var clock = new ExpressionDefinition
        {
            Time = "totalMinutes"
        };

        var action = new ActionDefinition
        {
            Id = "citizen_mind.wait_until",
            Handler = "citizen_mind.wait_until",

            Parameters = new()
            {
                ["untilMinutes"] = new ParameterDefinition
                {
                    Type = ParameterType.DOUBLE,
                    Value = until.TotalMinutes.ToString("R", CultureInfo.InvariantCulture)
                }
            },

            Duration = new DurationDefinition
            {
                Unit = "simulationMinutes",
                Estimate = new ExpressionDefinition
                {
                    Op = "max",
                    Args =
                    [
                        new() { Literal = 0 },
                        new()
                        {
                            Op = "subtract",
                            Args = [target, clock]
                        }
                    ]
                }
            },

            Prediction = [],

            Completion =
            [
                new()
                {
                    Op = "gte",
                    Args = [clock, target]
                }
            ],

            Cost = new CostDefinition
            {
                Expression = new ExpressionDefinition
                {
                    Calculated = "durationMinutes"
                }
            }
        };

        action.ResolvedParameters = ParameterParser.ParseAll(action.Parameters, action.Id);

        return new ActionCandidate
        {
            Id = $"citizen_mind.wait_until:{until.Ticks}",
            Definition = action
        };
    }
}