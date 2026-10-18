using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Needs.Components;
using CitizenSimulator.Needs.Services.Abstractions;

namespace CitizenSimulator.Needs.Services;

public class NeedsService(IEntityStore store, IEventBus events) : INeedsService
{
    public bool SetHunger(NeedsComponent needsComponent, float hunger)
    {
        throw new NotImplementedException();
    }

    public bool SetThirst(NeedsComponent needsComponent, float thirst)
    {
        throw new NotImplementedException();
    }

    public bool SetEnergy(NeedsComponent needsComponent, float energy)
    {
        throw new NotImplementedException();
    }

    public bool AddHunger(NeedsComponent needsComponent, float hunger)
    {
        throw new NotImplementedException();
    }

    public bool AddThirst(NeedsComponent needsComponent, float thirst)
    {
        throw new NotImplementedException();
    }

    public bool AddEnergy(NeedsComponent needsComponent, float energy)
    {
        throw new NotImplementedException();
    }
}