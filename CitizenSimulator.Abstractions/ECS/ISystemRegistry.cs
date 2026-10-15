namespace CitizenSimulator.Abstractions.ECS;

public interface ISystemRegistry
{
    void Register(GameSystem system);

    bool IsRegistered<T>() where T : GameSystem;
    
    IReadOnlyList<GameSystem> GetSystems();
}