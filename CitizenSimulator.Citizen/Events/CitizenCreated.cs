using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Citizen.Components;

namespace CitizenSimulator.Citizen.Events;

public sealed record CitizenCreated(CitizenComponent citizen) : IEvent;
