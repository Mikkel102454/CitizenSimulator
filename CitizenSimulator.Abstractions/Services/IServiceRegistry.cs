namespace CitizenSimulator.Abstractions.Services;

public interface IServiceRegistry
{
    void Register<T>(T service) where T : class;

    T Get<T>() where T : class;

    bool TryGet<T>(out T? service) where T : class;
}
