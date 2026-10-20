using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Timing;

namespace CitizenSimulator.Abstractions.Events;

public delegate void EventHandler<TEvent>(TEvent eventData, SimulationTime time, ICommandBuffer commands) where TEvent : IEvent;