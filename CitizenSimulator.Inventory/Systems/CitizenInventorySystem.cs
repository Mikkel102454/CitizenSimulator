using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Citizen.Events;
using CitizenSimulator.Inventory.Components;

namespace CitizenSimulator.Inventory.Systems;

public sealed class CitizenInventorySystem(IEventBus events) : GameSystem
{
    public const decimal DefaultCapacity = 20;
    private IDisposable? _citizenCreated;

    protected override void OnStart(ICommandBuffer commands)
    {
        _citizenCreated = events.Subscribe<OnCitizenCreated>(OnCitizenCreated);
        foreach (EntityRef entity in Store.Query(CitizenComponent.GetComponentType()))
            EnsureInventory(entity, commands);
    }

    private void OnCitizenCreated(OnCitizenCreated created, SimulationTime time, ICommandBuffer commands)
    {
        EnsureInventory(created.Citizen.Parent, commands);
    }

    private void EnsureInventory(EntityRef citizen, ICommandBuffer commands)
    {
        if (Store.IsAlive(citizen) && Store.GetComponent<CitizenComponent>(citizen) is { Alive: true }
            && !Store.HasComponent<InventoryComponent>(citizen))
            commands.AddComponent(citizen, new InventoryComponent { Capacity = DefaultCapacity });
    }

    protected override void OnStop(ICommandBuffer commands)
    {
        _citizenCreated?.Dispose();
    }
}
