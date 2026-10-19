using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Calender.Components;

namespace CitizenSimulator.Calender.Events;

public sealed record OnDayStart(CalenderComponent.Date Date) : IEvent;
