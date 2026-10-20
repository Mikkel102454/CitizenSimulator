using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Needs.Components;
using CitizenSimulator.Needs.Services.Abstractions;

namespace CitizenSimulator.Needs.Services;

public class NeedsService : INeedsService
{
    public bool SetHunger(NeedsComponent needsComponent, double hunger)
    {
        throw new NotImplementedException();
    }

    public bool SetThirst(NeedsComponent needsComponent, double thirst)
    {
        throw new NotImplementedException();
    }

    public bool SetEnergy(NeedsComponent needsComponent, double energy)
    {
        throw new NotImplementedException();
    }

    public bool AddHunger(NeedsComponent needsComponent, double hunger)
    {
        throw new NotImplementedException();
    }

    public bool AddThirst(NeedsComponent needsComponent, double thirst)
    {
        throw new NotImplementedException();
    }

    public bool AddEnergy(NeedsComponent needsComponent, double energy)
    {
        throw new NotImplementedException();
    }
}