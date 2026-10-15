using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Abstractions.Timing;

namespace CitizenSimulator.Engine.Events;

public class EventBus : IEventBus
{
    private readonly Queue<(Type EventType, IEvent Payload)> _pendingEvents = new();
    
    private readonly Dictionary<Type, List<ISubscription>> _subscriptions = new();
    
    private interface ISubscription : IDisposable
    {
        void Invoke(IEvent eventData, SimulationTime time, ICommandBuffer commandBuffer);
    }
    
    private sealed class Subscription<TEvent>(CitizenSimulator.Abstractions.Events.EventHandler<TEvent> handler, Action<ISubscription> unsubscribe) : ISubscription where TEvent : IEvent
    {
        private bool _disposed;

        public void Invoke(IEvent eventData, SimulationTime time, ICommandBuffer commandBuffer)
        {
            if (_disposed)
                return;

            handler((TEvent)eventData, time, commandBuffer);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            unsubscribe(this);
        }
    }
    
    public void Publish<TEvent>(TEvent eventData) where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(eventData);
        
        _pendingEvents.Enqueue((typeof(TEvent), eventData));
    }

    public IDisposable Subscribe<TEvent>(Abstractions.Events.EventHandler<TEvent> handler) where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        var eventType = typeof(TEvent);

        if (!_subscriptions.TryGetValue(eventType, out var listeners))
        {
            listeners = new List<ISubscription>();
            _subscriptions.Add(eventType, listeners);
        }
        
        var subscription = new Subscription<TEvent>(handler, item => listeners.Remove(item));

        listeners.Add(subscription);

        return subscription;
    }
    
    internal void Dispatch(SimulationTime time, Action<Action<ICommandBuffer>> executeCallback)
    {
        var batch = _pendingEvents.ToArray();
        _pendingEvents.Clear();

        foreach (var (eventType, payload) in batch)
        {
            if (!_subscriptions.TryGetValue(eventType, out var listeners))
                continue;

            var snapshot = listeners.ToArray();

            foreach (var subscription in snapshot)
            {
                executeCallback(buffer => subscription.Invoke(payload, time, buffer));
            }
        }
    }
}