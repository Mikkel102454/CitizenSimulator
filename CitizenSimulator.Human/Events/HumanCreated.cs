using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;

namespace CitizenSimulator.Human.Events;

public sealed record HumanCreated(EntityRef Entity) : IEvent;
