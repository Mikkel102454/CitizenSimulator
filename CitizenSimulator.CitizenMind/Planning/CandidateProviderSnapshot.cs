using CitizenSimulator.CitizenMind.Definitions;
using CitizenSimulator.CitizenMind.Planning.Abstractions;

namespace CitizenSimulator.CitizenMind.Planning;

public sealed class CandidateProviderSnapshot
{
    internal PlanningMeasurement? Measurement { get; init; }
    public required CandidateProviderDescriptor Descriptor { get; init; }

    public required IActionCandidateProvider Provider { get; init; }
}
