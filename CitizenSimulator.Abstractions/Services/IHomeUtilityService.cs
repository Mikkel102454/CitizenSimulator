using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Abstractions.Services;

public interface IHomeUtilityService
{
    bool HasWater(EntityRef house);
    bool HasElectricity(EntityRef house);
    bool TryConsumeDrinkingWater(EntityRef citizen, decimal litres);
    decimal ConsumeDrinkingWater(EntityRef citizen, decimal requestedLitres);
}
