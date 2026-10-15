namespace CitizenSimulator.Abstractions.Events;

// where the magic happens later
public interface IEventBus
{
    void Publish<TEvent>(TEvent eventData) where TEvent : IEvent;

    IDisposable Subscribe<TEvent>(EventHandler<TEvent> handler) where TEvent : IEvent;
}