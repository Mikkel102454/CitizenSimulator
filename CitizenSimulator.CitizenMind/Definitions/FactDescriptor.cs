namespace CitizenSimulator.CitizenMind.Definitions;

public sealed class FactDescriptor
{
    public required string Id { get; init; }
    public required Type ValueType { get; init; }
    public bool CanPredict { get; init; } = false;
}