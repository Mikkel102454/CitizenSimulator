using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.CitizenMind.Models;

public sealed record ActionContext(EntityRef Citizen, TimeSpan Elapsed, TimeSpan Delta);