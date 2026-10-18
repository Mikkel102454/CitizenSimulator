using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Citizen.Components;

namespace CitizenSimulator.Housing.Components;

public sealed class HouseComponent : Component<HouseComponent>
{
    public uint MaxOccupants { get; internal set; } = 0;
    public List<CitizenComponent> Occupants { get; internal set; } = [];
}