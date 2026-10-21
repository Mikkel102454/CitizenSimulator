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
        ArgumentNullException.ThrowIfNull(needsComponent);

        if (!double.IsFinite(thirst))
            return false;

        needsComponent.Thirst = Math.Clamp(thirst, 0.0, 1.0);
        return true;
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
        ArgumentNullException.ThrowIfNull(needsComponent);

        if (!double.IsFinite(thirst))
            return false;

        return SetThirst(needsComponent, needsComponent.Thirst + thirst);
    }

    public bool AddEnergy(NeedsComponent needsComponent, double energy)
    {
        throw new NotImplementedException();
    }
}