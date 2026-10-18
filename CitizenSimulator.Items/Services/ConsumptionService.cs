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
        if (!item.Consumable)
            return false;

        foreach (var effect in item.Effects)
        {
            switch (effect.Type)
            {
                case "needs.modify":
                    ModifyNeeds(effect, store.GetComponent<NeedsComponent>(citizen.Parent));
                    break;
            }
        }

        return true;
    }

    private void ModifyNeeds(Effect effect, NeedsComponent? needs)
    {
        if(needs == null)
            return;
        
        switch (effect.Values["need"].GetString())
        {
            case "hunger":
                needsService.AddHunger(needs, effect.Values["amount"].GetSingle());
                break;
            case "thirst":
                needsService.AddThirst(needs, effect.Values["amount"].GetSingle());
                break;
            case "energy":
                needsService.AddThirst(needs, effect.Values["amount"].GetSingle());
                break;
        }
    }
}