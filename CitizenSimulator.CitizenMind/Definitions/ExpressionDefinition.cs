using CitizenSimulator.CitizenMind.Components;
using CitizenSimulator.CitizenMind.Services;

namespace CitizenSimulator.CitizenMind.Models;

public sealed class ExpressionDefinition
{
    public string? Fact { get; init; }

    public string? Binding { get; init; }

    public object? Literal { get; init; }

    public string? Op { get; init; }

    public List<ExpressionDefinition> Args { get; init; } = [];
    
    public string? Before { get; init; }
    
    // only for actions
    public string? Parameter { get; init; }
    
    public string? Calculated { get; init; }
}