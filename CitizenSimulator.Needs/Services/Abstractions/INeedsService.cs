using CitizenSimulator.Needs.Components;

namespace CitizenSimulator.Needs.Services.Abstractions;

public interface INeedsService
{
    bool SetHunger(NeedsComponent needsComponent, double hunger);
    bool SetThirst(NeedsComponent needsComponent, double thirst);
    bool SetEnergy(NeedsComponent needsComponent, double energy);
    
    bool AddHunger(NeedsComponent needsComponent, double hunger);
    bool AddThirst(NeedsComponent needsComponent, double thirst);
    bool AddEnergy(NeedsComponent needsComponent, double energy);
}