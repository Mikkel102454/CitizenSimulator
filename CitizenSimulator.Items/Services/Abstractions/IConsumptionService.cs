using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Items.Models;

namespace CitizenSimulator.Items.Services.Abstractions;

public interface IConsumptionService
{
    bool ConsumeItem(Item item, CitizenComponent citizen);
}