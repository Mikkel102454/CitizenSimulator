using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.CitizenMind.Models;

public sealed record ActionContext(EntityRef Citizen, TimeSpan Elapsed, TimeSpan Delta)
{

    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();

    public IReadOnlyDictionary<string, object?> Bindings { get; init; } = new Dictionary<string, object?>();

    public TimeSpan GameTime { get; init; }

    public T GetParameter<T>(string name)
    {
        if (!Parameters.TryGetValue(name, out var value) || value is not T typed)
            throw new InvalidOperationException($"Parameter '{name}' must have type '{typeof(T).Name}'.");

        return typed;
    }

}
