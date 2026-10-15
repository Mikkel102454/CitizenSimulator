namespace CitizenSimulator.Abstractions.Events;

public interface IEventBus
{
    void Publish<TEvent>(TEvent eventData) where TEvent : IEvent;

    IDisposable Subscribe<TEvent>(EventHandler<TEvent> handler) where TEvent : IEvent;
}