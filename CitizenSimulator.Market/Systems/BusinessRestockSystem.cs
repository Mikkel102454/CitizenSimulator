using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Abstractions.Services;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Models;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.Market.Services.Abstractions;
using CitizenSimulator.Items.Services.Abstractions;

namespace CitizenSimulator.Market.Systems;

// Procurement runs after business updates, including when production is temporarily blocked.
public sealed class BusinessRestockSystem(IMarketService market,
    IBusinessService businesses,
    IItemService items, IServiceRegistry? services = null) : GameSystem(priority: 10)
{
    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {
        var allBusinesses = Store.Query(BusinessComponent.GetComponentType())
            .OrderBy(entity => entity.Id).Select(entity => Store.GetComponent<BusinessComponent>(entity)!)
            .ToArray();

        foreach (var business in allBusinesses)
        {
            if (business.State is not (BusinessState.Open or BusinessState.Closed) || business.Property.Destroyed)
                continue;

            var recipes = businesses.GetRecipes().Where(recipe => business.Type.Recipes.Contains(recipe.Id)).ToArray();
            RestockInventory(business, recipes);
            if (business.GridConnected)
                RestockUtilities(business, recipes, allBusinesses);

            DisposeWaste(business, recipes, allBusinesses);
        }
    }

    private void RestockInventory(BusinessComponent business, Recipe[] recipes)
    {
        var policies = business.Type.StockPolicy.ToDictionary(policy => policy.ItemId);
        foreach (var recipe in recipes)
        {
            foreach (var (itemId, quantity) in recipe.Inputs)
            {
                policies.TryGetValue(itemId, out var existing);
                policies[itemId] = new StockPolicy
                {
                    ItemId = itemId,
                    ReorderBelow = Math.Max(existing?.ReorderBelow ?? 0, quantity),
                    TargetQuantity = Math.Max(existing?.TargetQuantity ?? 0, quantity * 2)
                };
            }
        }

        foreach (var policy in policies.Values)
        {
            decimal quantity = business.Inventory.GetQuantity(policy.ItemId) + business.Inventory.GetReservedQuantity(policy.ItemId);
            decimal target = market.GetRestockTarget(business, policy.ItemId, policy.TargetQuantity);
            decimal reorderBelow = policy.ReorderBelow;
            if (target > policy.TargetQuantity)
                reorderBelow = Math.Max(reorderBelow, target / 2);

            if (quantity >= reorderBelow)
                continue;

            decimal shortage = target - quantity;
            if (services != null && services.TryGet<ICitySupplyService>(out var supply))
                supply!.ReplenishStock(business.Parent, policy.ItemId, shortage);
            else
                market.TryBuyUpTo(business.Inventory, business.PaymentAccount, policy.ItemId, shortage);
        }
    }

    private void RestockUtilities(BusinessComponent business, Recipe[] recipes, BusinessComponent[] allBusinesses)
    {
        decimal batchElectricity = recipes.Length == 0 ? 0 : recipes.Max(recipe => recipe.UtilitiesPerBatch.ElectricityKwh);
        decimal batchWater = recipes.Length == 0 ? 0 : recipes.Max(recipe => recipe.UtilitiesPerBatch.WaterLitres);
        decimal electricityTarget = business.Type.Utilities.ElectricityKwhPerOperatingHour * business.UtilityBufferHours + batchElectricity;
        decimal waterTarget = business.Type.Utilities.WaterLitresPerOperatingHour * business.UtilityBufferHours + batchWater;
        BuyUtility(business, "electricity", electricityTarget, allBusinesses);
        BuyUtility(business, "water", waterTarget, allBusinesses);
    }

    private void BuyUtility(BusinessComponent business, string utility, decimal target, BusinessComponent[] allBusinesses)
    {
        if (services != null && services.TryGet<ICitySupplyService>(out var supply))
        {
            supply!.SupplyUtility(business.Parent, utility, target);
            return;
        }

        var suppliers = allBusinesses
            .Where(supplier => !ReferenceEquals(supplier, business)
            && supplier.Type.GridSupplyPolicy?.Utility == utility)
            .OrderBy(supplier => supplier.Type.GridSupplyPolicy!.UnitPrice)
            .ThenBy(supplier => supplier.Parent.Id);
        foreach (var supplier in suppliers)
        {
            decimal available = utility == "electricity" ? business.ElectricityKwh : business.WaterLitres;
            decimal shortage = target - available;
            if (shortage <= 0)
                break;

            businesses.TryTransferUtility(supplier, business, utility, shortage, out _);
        }
    }

    private void DisposeWaste(BusinessComponent business, Recipe[] recipes, BusinessComponent[] allBusinesses)
    {
        var fuelInputs = recipes.SelectMany(recipe => recipe.Inputs.Keys).ToHashSet();
        var wasteIds = recipes.SelectMany(recipe => recipe.WasteOutputs.Keys)
            .Concat(business.Inventory.Inventory.Keys.Where(itemId => itemId.StartsWith("waste.", StringComparison.Ordinal)))
            .Where(itemId => !fuelInputs.Contains(itemId)).Distinct().ToArray();
        foreach (var itemId in wasteIds)
        {
            if (services != null && services.TryGet<ICitySupplyService>(out var supply))
            {
                supply!.CollectWaste(business.Parent, itemId);
                continue;
            }

            var landfills = allBusinesses
                .Where(landfill => !ReferenceEquals(landfill, business)
                && landfill.Type.DisposalPolicy?.AcceptedItems.Contains(itemId) == true)
                .OrderBy(landfill => landfill.Type.DisposalPolicy!.FeePerTonne)
                .ThenBy(landfill => landfill.Parent.Id);
            foreach (var landfill in landfills)
            {
                decimal quantity = business.Inventory.GetQuantity(itemId);
                if (quantity <= 0)
                    break;

                var item = items.GetItems().Single(item => item.Id == itemId);
                if (item.MassKgPerUnit is not > 0)
                    continue;

                decimal tonnesPerUnit = item.MassKgPerUnit.Value / 1000;
                decimal feePerTonne = landfill.Type.DisposalPolicy!.FeePerTonne;

                quantity = Math.Min(quantity, businesses.GetDisposalCapacity(landfill, itemId));

                if (feePerTonne > 0)
                {
                    decimal affordableQuantity = business.PaymentAccount.Balance / (feePerTonne * tonnesPerUnit);
                    quantity = Math.Min(quantity, affordableQuantity);
                }

                quantity = Math.Floor(quantity / item.QuantityStep) * item.QuantityStep;
                if (quantity > 0)
                    businesses.TryDisposeWaste(business, landfill, itemId, quantity);
            }
        }
    }
}
