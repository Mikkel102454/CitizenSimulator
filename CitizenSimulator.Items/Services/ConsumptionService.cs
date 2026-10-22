using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Items.Models;
using CitizenSimulator.Items.Services.Abstractions;
using CitizenSimulator.Needs.Components;
using CitizenSimulator.Needs.Services.Abstractions;

namespace CitizenSimulator.Items.Services;

public class ConsumptionService(IEntityStore store, IEventBus events, INeedsService needsService) : IConsumptionService
{
    public bool ConsumeItem(Item item, CitizenComponent citizen)
    {
        if (!store.IsAlive(citizen.Parent) || !citizen.Alive ||
            !ConsumptionEffects.TryRead(item, out _))
            return false;
        var needs = store.GetComponent<NeedsComponent>(citizen.Parent);
        if (needs == null || !double.IsFinite(needs.Hunger) || !double.IsFinite(needs.Thirst) ||
            !double.IsFinite(needs.Energy)) return false;
        // Validate all effects before mutating. Inventory owns the actual portion.
        foreach (var effect in item.Effects)
        {
            if (effect.Type == "needs.modify")
                ModifyNeeds(effect, needs);
        }
        return true;
    }

    private void ModifyNeeds(Effect effect, NeedsComponent needs)
    {
        double amount = effect.Values["amount"].GetDouble();
        switch (effect.Values["need"].GetString())
        {
            case "hunger":
                needsService.AddHunger(needs, amount);
                break;
            case "thirst":
                needsService.AddThirst(needs, amount);
                break;
            case "energy":
                needsService.AddEnergy(needs, amount);
                break;
        }
    }
}
