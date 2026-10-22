using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Property.Components;

namespace CitizenSimulator.Housing.Components;

public sealed class HouseComponent : Component<HouseComponent>
{
    private readonly List<CitizenComponent> _occupants = [];

    public PropertyComponent Property { get; internal set; } = null!;
    public uint MaxOccupants { get; internal set; }
    public IReadOnlyList<CitizenComponent> Occupants => _occupants.AsReadOnly();
    public bool HasBeds { get; internal set; }
    public bool HasSafeWater { get; internal set; }
    public bool HasKitchen { get; internal set; }
    public decimal StorageCapacity { get; internal set; }

    internal void AddOccupant(CitizenComponent citizen) => _occupants.Add(citizen);
    internal void RemoveOccupant(CitizenComponent citizen) => _occupants.Remove(citizen);
}
