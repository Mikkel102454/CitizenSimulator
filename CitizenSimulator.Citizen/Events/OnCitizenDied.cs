using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Citizen.Components;

namespace CitizenSimulator.Citizen.Events;

public sealed record OnCitizenDied(CitizenComponent Citizen) : IEvent;
