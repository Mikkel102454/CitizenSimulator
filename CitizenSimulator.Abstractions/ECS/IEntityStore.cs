using CitizenSimulator.Abstractions.ECS.Queries;

namespace CitizenSimulator.Abstractions.ECS;

// entitystore hols all entities and their components
public interface IEntityStore
{
    EntityRef CreateEntity();

    void DestroyEntity(EntityRef entity);

    bool IsAlive(EntityRef entity);
    
    void AddComponent<T>(EntityRef entity, T component) where T : class, IComponent;

    T? GetComponent<T>(EntityRef entity) where T : class, IComponent;

    bool HasComponent<T>(EntityRef entity) where T : class, IComponent;

    void RemoveComponent<T>(EntityRef entity) where T : class, IComponent;

    IEnumerable<EntityRef> Query(IQuery query);
}