namespace CitizenSimulator.Abstractions.ECS;

// registry for all game systems
public interface ISystemRegistry
{
    void Register(GameSystem system);

    bool IsRegistered<T>() where T : GameSystem;
    
    IReadOnlyList<GameSystem> GetSystems();
}