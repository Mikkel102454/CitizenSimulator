using CitizenSimulator.Abstractions.Services;

namespace CitizenSimulator.Engine.Services;

public sealed class ServiceRegistry : IServiceRegistry
{
    private readonly Dictionary<Type, object> _services = new();

    public void Register<T>(T service) where T : class
    {
        ArgumentNullException.ThrowIfNull(service);

        if (!_services.TryAdd(typeof(T), service))
        {
            throw new InvalidOperationException($"Service '{typeof(T).FullName}' is already registered.");
        }
    }

    public T Get<T>() where T : class
    {
        if (TryGet<T>(out var service))
            return service!;

        throw new InvalidOperationException($"Service '{typeof(T).FullName}' is not registered.");
    }

    public bool TryGet<T>(out T? service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var value))
        {
            service = (T)value;
            return true;
        }

        service = null;
        return false;
    }
}
