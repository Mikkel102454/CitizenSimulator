namespace CitizenSimulator.CitizenMind.Definitions;

public sealed class CandidateProviderDescriptor
{
    public required string Id { get; init; }

    public required IReadOnlyDictionary<string, Type> BindingTypes { get; init; }
}