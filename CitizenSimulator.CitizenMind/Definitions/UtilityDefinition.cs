using CitizenSimulator.CitizenMind.Components;
using CitizenSimulator.CitizenMind.Services;

namespace CitizenSimulator.CitizenMind.Models;


public sealed class UtilityDefinition
{
    public double Bias { get; init; } = 1.0;

    public required List<ConsiderationDefinition> Considerations { get; init; }
}