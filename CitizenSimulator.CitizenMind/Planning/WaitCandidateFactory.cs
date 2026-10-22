using System.Collections.Concurrent;
using System.Globalization;
using CitizenSimulator.CitizenMind.Configuration;
using CitizenSimulator.CitizenMind.Models;

namespace CitizenSimulator.CitizenMind.Planning;

internal static class WaitCandidateFactory
{
    private static readonly ConcurrentDictionary<long, ActionDefinition> Definitions = new();

    public static ActionCandidate Create(TimeSpan until)
    {
        // Reuse immutable YAML definitions for common opening times; keep this cache bounded.
        if (Definitions.Count >= 128)
            Definitions.Clear();
        var action = Definitions.GetOrAdd(until.Ticks, ticks => MindDefinitionLoader.ReadAction(
            typeof(WaitCandidateFactory).Assembly, "CitizenSimulator.CitizenMind.Data.Actions.wait_until.yml",
            new Dictionary<string, string>
            {
                ["untilMinutes"] = TimeSpan.FromTicks(ticks).TotalMinutes.ToString("R", CultureInfo.InvariantCulture)
            }));
        return new ActionCandidate { Id = $"{action.Id}:{until.Ticks}", Definition = action };
    }
}
