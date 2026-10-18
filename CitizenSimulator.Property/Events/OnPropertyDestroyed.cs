using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Property.Components;

namespace CitizenSimulator.Property.Events;

public sealed record OnPropertyDestroyed(PropertyComponent Property) : IEvent;
