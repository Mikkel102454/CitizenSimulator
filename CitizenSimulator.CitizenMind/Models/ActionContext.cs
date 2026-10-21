using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.CitizenMind.Models;

public sealed record ActionContext(EntityRef Citizen, TimeSpan Elapsed, TimeSpan Delta)
{
    public IReadOnlyDictionary<string, double> Parameters { get; init; } = new Dictionary<string, double>();
}