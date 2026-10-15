using System.Reflection;
using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Engine.ECS;

public sealed class ComponentRegistry : IComponentRegistry
{
    private readonly Dictionary<Type, int> _types = new();

    private int _nextId = 1;

    // register a single component
    public ComponentType<T> Register<T>() where T : class, IComponent
    {
        Type type = typeof(T);

        if (_types.TryGetValue(type, out int existingId))
            return new ComponentType<T>(existingId);

        int id = _nextId++;
        _types.Add(type, id);

        return new ComponentType<T>(id);
    }

    // automatically register components from an assembly
    public void RegisterFromAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsClass || !typeof(IComponent).IsAssignableFrom(type) || type.IsAbstract || type.ContainsGenericParameters)
                continue;

            var method = GetType().GetMethod(nameof(Register))!.MakeGenericMethod(type);

            method.Invoke(this, null);
        }
    }


    // get a component we have registred and throw if we dont have it
    public ComponentType<T> Get<T>() where T : class, IComponent
    {
        if (!_types.TryGetValue(typeof(T), out int id))
        {
            throw new InvalidOperationException($"Component {typeof(T).Name} is not registered.");
        }

        return new ComponentType<T>(id);
    }

    // returns if the component has been registered duuuh
    public bool IsRegistered<T>() where T : class, IComponent
    {
        return _types.ContainsKey(typeof(T));
    }
}