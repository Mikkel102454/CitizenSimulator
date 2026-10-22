using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Utilities.Models;

namespace CitizenSimulator.Utilities.Services;

public sealed partial class UtilityService
{
    public decimal CollectWaste(EntityRef entity, string itemId)
    {
        var item = items.GetItems().FirstOrDefault(item => item.Id == itemId);
        if (item == null || !itemId.StartsWith("waste.", StringComparison.Ordinal) || item.MassKgPerUnit is not > 0
            || !AccountsReady || GetBusiness(entity) is not { } business || !CanSupply(business))
            return 0;

        RefreshLimits();
        decimal original = business.Inventory.GetQuantity(itemId);
        decimal tonnesPerUnit = item.MassKgPerUnit.Value / 1000;
        decimal collectionLeft = Math.Max(0, Configuration.CollectionTonnesPerDay - _collectedToday);
        decimal allowed = Math.Min(original, collectionLeft / tonnesPerUnit);
        decimal collected = 0;
        SupplyStatistics statistics = Statistics(itemId);
        var facilities = GetBusinesses().Where(facility => !ReferenceEquals(facility, business)
            && facility.Type.DisposalPolicy?.AcceptedItems.Contains(itemId) == true && CanSupply(facility))
            .OrderBy(facility => facility.Type.DisposalPolicy!.PermanentStorage)
            .ThenBy(facility => facility.Type.DisposalPolicy!.FeePerTonne).ThenBy(facility => facility.Parent.Id);
        foreach (BusinessComponent facility in facilities)
        {
            decimal quantity = Math.Min(allowed - collected, businesses.GetDisposalCapacity(facility, itemId));
            decimal price = facility.Type.DisposalPolicy!.FeePerTonne * tonnesPerUnit;
            quantity = RoundDown(Affordable(business, quantity, price), item.QuantityStep);
            if (quantity <= 0)
                continue;

            if (businesses.TryDisposeWaste(business, facility, itemId, quantity))
            {
                collected += quantity;
                statistics.DisposalFees += quantity * price;
            }

            if (collected >= allowed)
                break;
        }

        decimal exported = ExportWaste(business, itemId, allowed - collected, tonnesPerUnit, item.QuantityStep);
        collected += exported;
        decimal tonnes = collected * tonnesPerUnit;
        _collectedToday += tonnes;
        statistics.CollectedTonnes += tonnes;
        // Waste shortages use kg/item quantities, while collection/disposal metrics use tonnes.
        _shortages[(entity, itemId, false)] = original - collected;
        return collected;
    }

    private decimal ExportWaste(BusinessComponent business, string itemId, decimal requested,
        decimal tonnesPerUnit, decimal step)
    {
        if (requested <= 0 || !Configuration.Waste.TryGetValue(itemId, out var policy) || !policy.Enabled)
            return 0;

        decimal dailyRemaining = Math.Max(0, policy.MaxTonnesPerDay - _wasteExportsToday.GetValueOrDefault(itemId));
        decimal capacityRemaining = Math.Max(0, policy.CapacityTonnes - _wasteExportedLifetime.GetValueOrDefault(itemId));
        decimal available = Math.Min(dailyRemaining, capacityRemaining) / tonnesPerUnit;
        decimal unitFee = policy.FeePerTonne * tonnesPerUnit;
        decimal amount = RoundDown(Affordable(business, Math.Min(requested, available), unitFee), step);
        if (amount <= 0 || !businesses.TryExportWaste(business, WorldAccount!, itemId, amount, amount * unitFee))
            return 0;

        decimal tonnes = amount * tonnesPerUnit;
        _wasteExportsToday[itemId] = _wasteExportsToday.GetValueOrDefault(itemId) + tonnes;
        _wasteExportedLifetime[itemId] = _wasteExportedLifetime.GetValueOrDefault(itemId) + tonnes;
        SupplyStatistics statistics = Statistics(itemId);
        statistics.WorldDisposedTonnes += tonnes;
        statistics.DisposalFees += amount * unitFee;
        return amount;
    }
}
