using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Calender.Components;

namespace CitizenSimulator.Calender.Events;

public sealed record OnWeekStart(CalenderComponent.Date Date) : IEvent;
