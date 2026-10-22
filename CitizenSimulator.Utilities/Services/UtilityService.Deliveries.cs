using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Utilities.Models;

namespace CitizenSimulator.Utilities.Services;

public sealed partial class UtilityService
{
    public decimal ReplenishStock(EntityRef entity, string itemId, decimal quantity)
    {
        var item = items.GetItems().FirstOrDefault(item => item.Id == itemId);
        if (item == null || item.QuantityStep <= 0 || quantity <= 0 || !AccountsReady
            || GetBusiness(entity) is not { } business || !CanSupply(business))
            return 0;

        RefreshLimits();
        quantity = RoundDown(quantity, item.QuantityStep);
        SupplyStatistics statistics = Statistics(itemId);
        statistics.Requested += quantity;
        var local = market.TryBuyUpTo(business.Inventory, business.PaymentAccount, itemId, quantity);
        statistics.LocalDelivered += local.Quantity;
        decimal shortage = quantity - local.Quantity;
        decimal imported = 0;
        if (shortage > 0 && Configuration.Items.TryGetValue(itemId, out var policy) && policy.Enabled)
        {
            decimal dailyRemaining = Math.Max(0, policy.MaxPerDay - _itemImportsToday.GetValueOrDefault(itemId));
            decimal amount = Math.Min(shortage, Math.Min(dailyRemaining, business.Inventory.FreeCapacity));
            amount = RoundDown(Affordable(business, amount, policy.UnitPrice), item.QuantityStep);
            if (amount > 0 && businesses.TryImportStock(business, WorldAccount!, itemId, amount, policy.UnitPrice))
            {
                imported = amount;
                _itemImportsToday[itemId] = _itemImportsToday.GetValueOrDefault(itemId) + amount;
                statistics.Imported += amount;
                statistics.ImportCost += amount * policy.UnitPrice;
                market.ResolveExternalDemand(business.Inventory, itemId, amount);
            }
        }

        _shortages[(entity, itemId, false)] = quantity - local.Quantity - imported;
        return local.Quantity + imported;
    }

    public decimal SupplyUtility(EntityRef entity, string utility, decimal targetQuantity)
    {
        if (targetQuantity < 0 || utility is not ("electricity" or "water") || !AccountsReady
            || GetBusiness(entity) is not { } business || !CanSupply(business))
            return 0;

        RefreshLimits();
        decimal requested = Math.Max(0, targetQuantity - Buffer(business, utility));
        SupplyStatistics statistics = Statistics(utility);
        statistics.Requested += requested;
        if (!business.GridConnected || !_connections.TryGetValue(entity, out var connection))
        {
            _shortages[(entity, utility, false)] = requested;
            return 0;
        }

        ResetConnectionTick(connection);
        decimal rate = utility == "electricity" ? connection.ElectricityKw : connection.WaterLitresPerHour;
        decimal alreadyDelivered = utility == "electricity" ? connection.ElectricityDelivered : connection.WaterDelivered;
        decimal allowed = Math.Max(0, PerTick(rate) - alreadyDelivered);
        decimal remaining = Math.Min(requested, allowed);
        decimal delivered = DeliverLocalUtility(business, utility, remaining);
        statistics.LocalDelivered += delivered;
        remaining -= delivered;
        decimal imported = ImportUtility(business, utility, remaining);
        delivered += imported;
        if (utility == "electricity")
            connection.ElectricityDelivered += delivered;
        else
            connection.WaterDelivered += delivered;

        _shortages[(entity, utility, false)] = requested - delivered;
        return delivered;
    }

    private decimal DeliverLocalUtility(BusinessComponent receiver, string utility, decimal quantity)
    {
        decimal delivered = 0;
        var suppliers = GetBusinesses().Where(supplier => !ReferenceEquals(supplier, receiver)
            && supplier.Type.GridSupplyPolicy?.Utility == utility && CanSupply(supplier))
            .OrderBy(supplier => supplier.Type.GridSupplyPolicy!.UnitPrice).ThenBy(supplier => supplier.Parent.Id);
        foreach (BusinessComponent supplier in suppliers)
        {
            decimal exportable = Math.Max(0, Buffer(supplier, utility) - UtilityTarget(supplier, utility));
            decimal requested = Math.Min(quantity - delivered, exportable);
            if (requested <= 0)
                continue;

            if (businesses.TryTransferUtility(supplier, receiver, utility, requested, out decimal transferred))
                delivered += transferred;

            if (delivered >= quantity)
                break;
        }

        return delivered;
    }

    private decimal ImportUtility(BusinessComponent receiver, string utility, decimal requested)
    {
        if (requested <= 0 || !Configuration.Utilities.TryGetValue(utility, out var policy) || !policy.Enabled)
            return 0;

        decimal capacity = PerTick(policy.MaxPerHour);
        decimal remaining = Math.Max(0, capacity - _utilityImportsThisTick.GetValueOrDefault(utility));
        decimal amount = Affordable(receiver, Math.Min(requested, remaining), policy.UnitPrice);
        if (amount <= 0 || !businesses.TryImportUtility(receiver, WorldAccount!, utility, amount, policy.UnitPrice))
            return 0;

        _utilityImportsThisTick[utility] = _utilityImportsThisTick.GetValueOrDefault(utility) + amount;
        SupplyStatistics statistics = Statistics(utility);
        statistics.Imported += amount;
        statistics.ImportCost += amount * policy.UnitPrice;
        return amount;
    }

    private decimal UtilityTarget(BusinessComponent business, string utility)
    {
        var recipes = businesses.GetRecipes().Where(recipe => business.Type.Recipes.Contains(recipe.Id)).ToArray();
        decimal hours = Math.Max(0, business.UtilityBufferHours);
        decimal rate = utility == "electricity"
            ? business.Type.Utilities.ElectricityKwhPerOperatingHour : business.Type.Utilities.WaterLitresPerOperatingHour;
        decimal batch = recipes.Length == 0 ? 0 : recipes.Max(recipe => utility == "electricity"
            ? recipe.UtilitiesPerBatch.ElectricityKwh : recipe.UtilitiesPerBatch.WaterLitres);
        return rate * hours + batch;
    }

    private static decimal Buffer(BusinessComponent business, string utility)
    {
        return utility == "electricity" ? business.ElectricityKwh : business.WaterLitres;
    }

    private void ResetConnectionTick(UtilityConnection connection)
    {
        if (connection.DeliveryTime == Now)
            return;

        connection.DeliveryTime = Now;
        connection.ElectricityDelivered = 0;
        connection.WaterDelivered = 0;
    }
}
