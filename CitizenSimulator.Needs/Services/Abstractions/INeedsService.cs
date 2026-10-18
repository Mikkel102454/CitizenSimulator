using CitizenSimulator.Needs.Components;

namespace CitizenSimulator.Needs.Services.Abstractions;

public interface INeedsService
{
    bool SetHunger(NeedsComponent needsComponent, float hunger);
    bool SetThirst(NeedsComponent needsComponent, float thirst);
    bool SetEnergy(NeedsComponent needsComponent, float energy);
    
    bool AddHunger(NeedsComponent needsComponent, float hunger);
    bool AddThirst(NeedsComponent needsComponent, float thirst);
    bool AddEnergy(NeedsComponent needsComponent, float energy);
}