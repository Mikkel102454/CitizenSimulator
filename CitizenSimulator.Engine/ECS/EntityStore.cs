
using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.ECS.Queries;

namespace CitizenSimulator.Engine.ECS;

public sealed class EntityStore : IEntityStore
{
    private readonly Dictionary<int, Dictionary<Type, IComponent>> _entities = new();

    private static int _nextStoreId = 0;

    private readonly int _storeId = Interlocked.Increment(ref _nextStoreId);
    
    private int _nextId = 1;

    private readonly HashSet<int> _reservedEntities = new ();
    
    private bool _isExecutingSystem;
    
    public EntityRef CreateEntity()
    {
        EnsureStructuralChangesAllowed();
        EntityRef entity = ReserveEntity();
        ActivateEntity(entity);
        return entity;
    }

    internal void BeginSystemExecution()
    {
        if (_isExecutingSystem)
        {
            throw new InvalidOperationException(
                "A system is already executing.");
        }

        _isExecutingSystem = true;
    }

    internal void EndSystemExecution()
    {
        _isExecutingSystem = false;
    }

    private void EnsureStructuralChangesAllowed()
    {
        if (_isExecutingSystem)
        {
            throw new InvalidOperationException(
                "Use the command buffer for structural changes during system execution.");
        }
    }
    
    internal bool OwnsReference(EntityRef entity)
    {
        return entity.StoreId == _storeId && entity.Generation == 1;
    }

    internal bool IsReserved(EntityRef entity)
    {
        return OwnsReference(entity) && _reservedEntities.Contains(entity.Id);
    }
    
    internal EntityRef ReserveEntity()
    {
        int id = _nextId++;
        EntityRef entity = new EntityRef(id, 1, _storeId);
        _reservedEntities.Add(entity.Id);
        return entity;
    }
    
    internal void CancelReservation(EntityRef entity)
    {
        if (IsReserved(entity))
        { 
            _reservedEntities.Remove(entity.Id);
            return;
        }
        throw new InvalidOperationException("Entity is not a valid pending reservation");
    }

    internal void ActivateEntity(EntityRef entity)
    {
        if (IsReserved(entity))
        { 
            _entities.Add(entity.Id, new Dictionary<Type, IComponent>());
            _reservedEntities.Remove(entity.Id);
            return;
        }
        throw new InvalidOperationException("Entity is not a valid pending reservation");
    }

    public void DestroyEntity(EntityRef entity)
    {
        EnsureStructuralChangesAllowed();
        Validate(entity);
        _entities.Remove(entity.Id);
    }

    public bool IsAlive(EntityRef entity)
    {
        return entity.StoreId == _storeId && entity.Generation == 1 && _entities.ContainsKey(entity.Id);
    }

    public void AddComponent<T>(EntityRef entity, T component) where T : class, IComponent
    {
        EnsureStructuralChangesAllowed();
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
        EnsureStructuralChangesAllowed();
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
