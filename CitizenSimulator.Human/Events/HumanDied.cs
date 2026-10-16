using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;

namespace CitizenSimulator.Human.Events;

public sealed record HumanDied(EntityRef Entity) : IEvent;
