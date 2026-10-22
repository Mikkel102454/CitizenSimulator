using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Models;
using CitizenSimulator.Market.Models;

namespace CitizenSimulator.Market.Services;

public partial class MarketService
{
    private readonly Dictionary<(EntityRef Seller, string ItemId), decimal> _smoothedDemand = [];

    public void AdvanceMarket(TimeSpan gameTime)
    {
        if (gameTime < _observedMarketTime)
            return;

        _observedMarketTime = gameTime;
        var cutoff = Now - TimeSpan.FromDays(1);
        _sales.RemoveAll(sale => sale.Time <= cutoff);
        _failures.RemoveAll(failure => failure.Time <= cutoff);
        _stockSamples.RemoveAll(sample => sample.Time <= cutoff);
        foreach (var key in _requests.Where(entry => entry.Value.LastObservedAt <= cutoff
            || entry.Value.RemainingQuantity == 0).Select(entry => entry.Key).ToArray())
        {
            _requests.Remove(key);
        }

        foreach (var request in _requests.Values)
        {
            RefreshRequest(request);
        }

        if (_lastPolicyUpdate is { } previous && gameTime - previous < TimeSpan.FromHours(1))
            return;

        var allBusinesses = store.Query(BusinessComponent.GetComponentType())
            .Select(entity => store.GetComponent<BusinessComponent>(entity)!)
            .Where(business => business.State is not (BusinessState.Bankrupt or BusinessState.ShutDown))
            .OrderBy(business => business.Parent.Id).ToArray();
        foreach (var business in allBusinesses)
        {
            if (business.Type.SalesPolicy is not { } policy)
                continue;

            foreach (string itemId in policy.SellItems.Keys)
            {
                _stockSamples.Add(new StockSample(gameTime, business.Parent, itemId, business.Inventory.GetQuantity(itemId)));
                // The first observation establishes stock, without inventing a prior hour of demand.
                if (_lastPolicyUpdate != null)
                    AdjustBusinessPolicy(business, itemId);
            }
        }

        var activeSellers = allBusinesses.Select(business => business.Parent).ToHashSet();
        foreach (var key in _smoothedDemand.Keys.Where(key => !activeSellers.Contains(key.Seller)).ToArray())
        {
            _smoothedDemand.Remove(key);
        }

        _lastPolicyUpdate = gameTime;
    }

    private decimal GetConfiguredTarget(BusinessComponent business, string itemId)
    {
        decimal configured = business.Type.StockPolicy.FirstOrDefault(policy => policy.ItemId == itemId)?.TargetQuantity ?? 0;
        foreach (var recipe in businesses.GetRecipes().Where(recipe => business.Type.Recipes.Contains(recipe.Id)))
        {
            if (recipe.Outputs.TryGetValue(itemId, out decimal quantity))
                configured = Math.Max(configured, business.Type.ProductionTargets.GetValueOrDefault(itemId, quantity * 2));
        }

        configured = Math.Max(configured, business.Type.ProductionTargets.GetValueOrDefault(itemId));
        return configured > 0 ? configured : Math.Max(2, GetQuantityStep(itemId));
    }

    private void AdjustBusinessPolicy(BusinessComponent business, string itemId)
    {
        var statistics = GetStatistics(itemId, business);
        // Inability to afford goods is reported, but must not be mistaken for a supply shortage.
        decimal affordablePending = statistics.PendingQuantity - statistics.UnmetQuantity;
        decimal effectiveDemand = statistics.SoldQuantity + statistics.ShortageQuantity + affordablePending;
        var key = (business.Parent, itemId);
        decimal previousDemand = _smoothedDemand.GetValueOrDefault(key, effectiveDemand);
        decimal demand = previousDemand * 0.75m + effectiveDemand * 0.25m;
        _smoothedDemand[key] = demand;

        decimal configuredTarget = GetConfiguredTarget(business, itemId);
        decimal desiredTarget = Math.Clamp(demand * 2, configuredTarget, configuredTarget * 4);
        decimal currentTarget = business.GetStockTarget(itemId, configuredTarget);
        decimal nextTarget = MoveTowards(currentTarget, desiredTarget, configuredTarget * 0.25m);

        decimal pressure = (demand - statistics.StockLevel) / Math.Max(configuredTarget, demand);
        decimal desiredPriceMultiplier = 1 + Math.Clamp(pressure, 0, 1);
        decimal nextPriceMultiplier = MoveTowards(business.GetPriceMultiplier(itemId), desiredPriceMultiplier, 0.05m);
        businesses.TrySetMarketAdjustment(business, itemId, nextTarget, nextPriceMultiplier);
    }

    private static decimal MoveTowards(decimal current, decimal desired, decimal maximumChange)
    {
        return current + Math.Clamp(desired - current, -maximumChange, maximumChange);
    }

    public decimal GetRestockTarget(BusinessComponent business, string itemId, decimal configuredTarget)
    {
        decimal target = business.GetStockTarget(itemId, configuredTarget);
        foreach (var recipe in businesses.GetRecipes().Where(recipe => business.Type.Recipes.Contains(recipe.Id)
            && recipe.Inputs.ContainsKey(itemId)))
        {
            decimal productionScale = 1;
            foreach (var (outputId, batchQuantity) in recipe.Outputs)
            {
                decimal baseline = business.Type.ProductionTargets.GetValueOrDefault(outputId, batchQuantity * 2);
                if (baseline > 0)
                    productionScale = Math.Max(productionScale, business.GetStockTarget(outputId, baseline) / baseline);
            }

            target = Math.Max(target, configuredTarget * productionScale);
        }

        return target;
    }
}
