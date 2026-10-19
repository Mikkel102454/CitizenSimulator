using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Calender.Components;

namespace CitizenSimulator.Calender.Events;

public sealed record OnMorningStart(CalenderComponent.Date Date) : IEvent;
