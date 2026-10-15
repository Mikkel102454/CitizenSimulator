using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Engine.ECS;

// registry for all our game systems
public sealed class SystemRegistry : ISystemRegistry
{
    private readonly List<GameSystem> _systems = new();

    private bool _isCacheGood; // if we add new systems cache should be invalid
    private IReadOnlyList<GameSystem> _systemCache = []; // our cache for systems
    public void Register(GameSystem system)
    {
        ArgumentNullException.ThrowIfNull(system);

        if (IsRegistered(system.GetType()))
            throw new InvalidOperationException($"System {system.Name} is already registered.");

        _isCacheGood = false;
        _systems.Add(system);
    }

    public bool IsRegistered<T>() where T : GameSystem
    {
        return IsRegistered(typeof(T));
    }

    private bool IsRegistered(Type type)
    {
        return _systems.Any(s => s.GetType() == type);
    }

    // get all systems sorted (pulls from cache if valid)
    public IReadOnlyList<GameSystem> GetSystems()
    {
        if (_isCacheGood)
        {
            return _systemCache;
        }

        _systemCache = _systems.OrderBy(s => s.Priority).ToArray();
        _isCacheGood = true;
        return _systemCache;
    }
}