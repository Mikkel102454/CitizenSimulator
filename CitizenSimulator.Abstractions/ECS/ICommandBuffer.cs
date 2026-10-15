namespace CitizenSimulator.Abstractions.ECS;

public interface ICommandBuffer
{
    EntityRef CreateEntity();
    
    void DestroyEntity(EntityRef entity);
    
    void TryDestroyEntity(EntityRef entity);
    
    T AddComponent<T>(EntityRef entity, T component) where T : class, IComponent;
    
    void RemoveComponent<T>(EntityRef entity) where T : class, IComponent;
}