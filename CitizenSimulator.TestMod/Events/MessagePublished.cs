using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;

namespace CitizenSimulator.TestMod.Events;

public sealed record MessagePublished(EntityRef Entity, string Message) : IEvent;