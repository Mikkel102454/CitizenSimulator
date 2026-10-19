using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Calender.Components;

namespace CitizenSimulator.Calender.Events;

public sealed record OnYearStart(CalenderComponent.Date Date) : IEvent;
