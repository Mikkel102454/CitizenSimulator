
using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.ECS.Queries;

namespace CitizenSimulator.Engine.ECS;

public sealed class EntityStore : IEntityStore
{
    private readonly Dictionary<int, Dictionary<Type, IComponent>> _entities = new();

    private static int _nextStoreId = 0;

    private readonly int _storeId = Interlocked.Increment(ref _nextStoreId);
    
    private int _nextId = 1;

    public EntityRef CreateEntity()
    {
        int id = _nextId++;
        _entities.Add(id, new Dictionary<Type, IComponent>());
        
        return new EntityRef(id, 1, _storeId);
    }

    public void DestroyEntity(EntityRef entity)
    {
        Validate(entity);
        _entities.Remove(entity.Id);
    }

    public bool IsAlive(EntityRef entity)
    {
        return entity.StoreId == _storeId && entity.Generation == 1 && _entities.ContainsKey(entity.Id);
    }

    public void AddComponent<T>(EntityRef entity, T component) where T : class, IComponent
    {
        Validate(entity);
        ArgumentNullException.ThrowIfNull(component);

        _entities[entity.Id][typeof(T)] = component;
    }

    public T? GetComponent<T>(EntityRef entity) where T : class, IComponent
    {
        Validate(entity);
        return _entities[entity.Id].TryGetValue(typeof(T), out var component) ? (T)component : null;
    }

    public bool HasComponent<T>(EntityRef entity) where T : class, IComponent
    {
        Validate(entity);
        return _entities[entity.Id].ContainsKey(typeof(T));
    }

    public void RemoveComponent<T>(EntityRef entity) where T : class, IComponent
    {
        Validate(entity);
        _entities[entity.Id].Remove(typeof(T));
    }
    
    public IEnumerable<EntityRef> Query(IQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        foreach (var pair in _entities)
        {
            if (query.Matches(pair.Value))
            {
                yield return new EntityRef(pair.Key, 1, _storeId);
            }
        }
    }


    private void Validate(EntityRef entity)
    {
        if (!IsAlive(entity))
        {
            throw new InvalidOperationException(
                $"Entity {entity.Id} is invalid or destroyed.");
        }
    }
}
