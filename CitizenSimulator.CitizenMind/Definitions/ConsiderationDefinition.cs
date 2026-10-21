using CitizenSimulator.CitizenMind.Components;
using CitizenSimulator.CitizenMind.Services;

namespace CitizenSimulator.CitizenMind.Models;

public sealed class ConsiderationDefinition
{
    public required ExpressionDefinition Input { get; init; }

    public double Weight { get; init; } = 1.0;
    public required List<double[]> Curve { get; init; }
}