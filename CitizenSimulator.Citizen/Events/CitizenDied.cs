using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Citizen.Components;

namespace CitizenSimulator.Citizen.Events;

public sealed record CitizenDied(CitizenComponent citizen) : IEvent;
