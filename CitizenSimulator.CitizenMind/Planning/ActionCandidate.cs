using CitizenSimulator.CitizenMind.Models;

namespace CitizenSimulator.CitizenMind.Planning;


public sealed class ActionCandidate
{
    public required string Id { get; init; }

    public required ActionDefinition Definition { get; init; }

    public IReadOnlyDictionary<string, object?> Bindings { get; init; } = new Dictionary<string, object?>();
}