using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;

namespace CitizenSimulator.Mod2.Events;

public sealed record MessagePublished(EntityRef Entity, string Message) : IEvent;