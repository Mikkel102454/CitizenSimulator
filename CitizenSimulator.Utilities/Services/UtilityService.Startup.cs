using CitizenSimulator.Business.Components;
using CitizenSimulator.Utilities.Models;

namespace CitizenSimulator.Utilities.Services;

public sealed partial class UtilityService
{
    private void Bootstrap(BusinessComponent business)
    {
        if (!Configuration.Startup.Enabled)
            return;

        var targets = StartupTargets(business);
        if (!business.StartupFundingAwarded)
        {
            decimal grant = StartupBudget(business, targets);
            if (!businesses.TryFundStartup(business, StartupFund!, grant))
                return;
        }

        foreach (var (itemId, target) in targets)
        {
            decimal available = business.Inventory.GetQuantity(itemId) + business.Inventory.GetReservedQuantity(itemId);
            if (available < target)
                ReplenishStock(business.Parent, itemId, target - available);
        }

        SupplyUtility(business.Parent, "electricity", UtilityTarget(business, "electricity"));
        SupplyUtility(business.Parent, "water", UtilityTarget(business, "water"));
    }

    private Dictionary<string, decimal> StartupTargets(BusinessComponent business)
    {
        Dictionary<string, decimal> targets = [];
        foreach (var policy in business.Type.StockPolicy)
        {
            targets[policy.ItemId] = policy.TargetQuantity;
        }

        foreach (var recipe in businesses.GetRecipes().Where(recipe => business.Type.Recipes.Contains(recipe.Id)))
        {
            foreach (var (itemId, amount) in recipe.Inputs)
            {
                targets[itemId] = Math.Max(targets.GetValueOrDefault(itemId), amount * 2);
            }

            foreach (var equipment in recipe.RequiredEquipment.GroupBy(equipment => equipment.EquipmentId))
            {
                targets[equipment.Key] = Math.Max(targets.GetValueOrDefault(equipment.Key), equipment.Sum(entry => entry.Count));
            }
        }

        foreach (var equipment in business.Type.Equipment.GroupBy(equipment => equipment.EquipmentId))
        {
            targets[equipment.Key] = Math.Max(targets.GetValueOrDefault(equipment.Key), equipment.Sum(entry => entry.Count));
        }

        return targets;
    }

    private decimal StartupBudget(BusinessComponent business, Dictionary<string, decimal> targets)
    {
        StartupPolicy policy = Configuration.Startup;
        decimal days = policy.WorkingCapitalDaysByBusiness.GetValueOrDefault(business.Type.Id, policy.WorkingCapitalDays);
        decimal dailyWages = business.Type.Workforce.Sum(role => role.DesiredCount * role.StartingHourlyWage) * 24;
        decimal dailyCosts = business.Type.OperatingCosts.InsurancePerDay + business.Type.OperatingCosts.MaintenanceBudgetPerDay;
        decimal energyPrice = Configuration.Utilities.GetValueOrDefault("electricity")?.UnitPrice ?? 0;
        decimal waterPrice = Configuration.Utilities.GetValueOrDefault("water")?.UnitPrice ?? 0;
        decimal dailyUtilities = 24 * (business.Type.Utilities.ElectricityKwhPerOperatingHour * energyPrice
            + business.Type.Utilities.WaterLitresPerOperatingHour * waterPrice);
        decimal reserve = Math.Max(business.Type.MinimumCapital, (dailyWages + dailyCosts + dailyUtilities) * days);
        decimal stockCost = targets.Sum(entry => Math.Max(0, entry.Value - business.Inventory.GetQuantity(entry.Key))
            * StartupUnitPrice(entry.Key));
        decimal bufferCost = UtilityTarget(business, "electricity") * energyPrice + UtilityTarget(business, "water") * waterPrice;
        return Math.Max(0, reserve + stockCost + bufferCost - business.PaymentAccount.Balance);
    }

    private decimal StartupUnitPrice(string itemId)
    {
        decimal localPrice = market.GetOffers(itemId, wholesale: true).FirstOrDefault()?.UnitPrice ?? 0;
        decimal worldPrice = Configuration.Items.GetValueOrDefault(itemId)?.UnitPrice ?? 0;
        return Math.Max(localPrice, worldPrice);
    }
}
