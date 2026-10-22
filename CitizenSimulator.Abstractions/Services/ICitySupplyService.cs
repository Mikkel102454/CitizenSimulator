using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Abstractions.Services;

// Runtime lookup keeps Market independent of the optional Utilities mod.
public interface ICitySupplyService
{
    decimal ReplenishStock(EntityRef business, string itemId, decimal quantity);
    decimal SupplyUtility(EntityRef business, string utility, decimal targetQuantity);
    decimal CollectWaste(EntityRef business, string itemId);
}
