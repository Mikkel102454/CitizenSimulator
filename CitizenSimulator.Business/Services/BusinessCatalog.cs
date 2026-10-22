using System.Globalization;
using System.Text.Json;
using CitizenSimulator.Business.Models;
using CitizenSimulator.Items.Services.Abstractions;
using BusinessDefinition = CitizenSimulator.Business.Models.Business;
using PropertyType = CitizenSimulator.Property.Components.PropertyComponent.PropertyType;

namespace CitizenSimulator.Business.Services;

public static class BusinessCatalog
{
    public static void Load(IItemService items)
    {
        var recipes = LoadDefinitions<Recipe>("CitizenSimulator.Business.Recipes.");
        var types = LoadDefinitions<BusinessDefinition>("CitizenSimulator.Business.Business.");
        Validate(recipes, types, items.GetItems().Select(item => item.Id).ToHashSet(StringComparer.Ordinal));
        Recipe.RecipeList.Clear();
        Recipe.RecipeList.AddRange(recipes);
        BusinessDefinition.BusinessList.Clear();
        BusinessDefinition.BusinessList.AddRange(types);
    }

    private static List<T> LoadDefinitions<T>(string prefix)
    {
        var assembly = typeof(BusinessCatalog).Assembly;
        var result = new List<T>();
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(resourceName => resourceName.StartsWith(prefix, StringComparison.Ordinal)
                && resourceName.EndsWith(".json", StringComparison.Ordinal))
            .Order();

        foreach (string resourceName in resourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var definition = JsonSerializer.Deserialize<T>(stream, options)
                    ?? throw new JsonException("Null definition.");
                result.Add(definition);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"Invalid business resource '{resourceName}'.", exception);
            }
        }

        return result;
    }

    public static bool ValidHours(OpeningHours hours)
    {
        return hours.Days.Count > 0
            && hours.Days.All(dayName => Enum.TryParse<DayOfWeek>(dayName, true, out var day) && Enum.IsDefined(day))
            && TimeOnly.TryParseExact(hours.Opens, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            && TimeOnly.TryParseExact(hours.Closes, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    public static void Validate(IReadOnlyList<Recipe> recipes,
        IReadOnlyList<BusinessDefinition> businesses,
        ISet<string> itemIds)
    {
        void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        void RequireValidEquipment(IEnumerable<EquipmentRequirement> requirements, string definitionId)
        {
            bool valid = requirements.All(requirement =>
                itemIds.Contains(requirement.EquipmentId) && requirement.Count > 0);
            Require(valid, $"{definitionId}: invalid equipment.");
        }

        void RequireValidQuantities(IReadOnlyDictionary<string, decimal> quantities, string definitionId)
        {
            bool valid = quantities.All(entry => itemIds.Contains(entry.Key) && entry.Value > 0);
            Require(valid, $"{definitionId}: invalid item/quantity.");
        }

        void RequireValidUtilities(BatchUtilities utilities, string definitionId)
        {
            bool valid = utilities.ElectricityKwh >= 0 && utilities.WaterLitres >= 0;
            Require(valid, $"{definitionId}: negative utilities.");
        }

        Require(itemIds.Count > 0, "Business catalog requires a loaded item catalog.");
        Require(recipes.All(recipe => !string.IsNullOrWhiteSpace(recipe.Id))
            && recipes
            .Select(recipe => recipe.Id).Distinct().Count() == recipes.Count, "Duplicate/empty recipe IDs.");
        Require(businesses.All(business => !string.IsNullOrWhiteSpace(business.Id))
            && businesses
            .Select(business => business.Id).Distinct().Count() == businesses.Count, "Duplicate/empty business IDs.");
        foreach (var recipe in recipes)
        {
            Require(recipe.DurationMinutes > 0 && recipe.LaborMinutes >= 0, $"{recipe.Id}: invalid time/labor.");
            RequireValidQuantities(recipe.Inputs, recipe.Id);
            RequireValidQuantities(recipe.Outputs, recipe.Id);
            RequireValidQuantities(recipe.WasteOutputs, recipe.Id);
            RequireValidEquipment(recipe.RequiredEquipment, recipe.Id);
            RequireValidUtilities(recipe.UtilitiesPerBatch, recipe.Id);
            RequireValidUtilities(recipe.UtilityOutputsPerBatch, recipe.Id);
            Require(recipe.Outputs.Count + recipe.WasteOutputs.Count > 0
                || recipe.UtilityOutputsPerBatch.ElectricityKwh + recipe.UtilityOutputsPerBatch.WaterLitres > 0, $"{recipe.Id}: no outputs.");
        }

        var recipeIds = recipes.Select(recipe => recipe.Id).ToHashSet();
        foreach (var business in businesses)
        {
            Require(business.Recipes.All(recipeIds.Contains), $"{business.Id}: unknown recipe.");
            Require(business.Premises.MinimumAreaM2 > 0
                && business.Premises.StorageCapacity >= 0
                && business.Premises.AllowedUses.Count > 0
                && business.Premises.AllowedUses.All(use =>
                    Enum.TryParse<PropertyType>(use, true, out var propertyType) && Enum.IsDefined(propertyType)),
                $"{business.Id}: invalid premises.");
            Require(business.OpeningHours.Count > 0
                && business.OpeningHours.All(ValidHours), $"{business.Id}: invalid hours.");
            Require(business.OperatingCosts.InsurancePerDay >= 0
                && business.OperatingCosts.MaintenanceBudgetPerDay >= 0
                && business.Utilities.ElectricityKwhPerOperatingHour >= 0
                && business.Utilities.WaterLitresPerOperatingHour >= 0
                && business.MinimumCapital >= 0
                && business.BankruptcyGraceDays >= 0, $"{business.Id}: invalid costs.");
            Require(business.Workforce
                .Select(requirement => requirement.Role).Distinct().Count() == business.Workforce.Count
                && business.Workforce.All(requirement => !string.IsNullOrWhiteSpace(requirement.Role)
                && requirement.DesiredCount >= 0
                && requirement.MinimumSkill >= 0
                && requirement.StartingHourlyWage > 0), $"{business.Id}: invalid workforce.");
            RequireValidEquipment(business.Equipment, business.Id);
            Require(business.StockPolicy.All(entry => itemIds.Contains(entry.ItemId)
                && entry.ReorderBelow >= 0
                && entry.TargetQuantity >= entry.ReorderBelow)
                && business.StockPolicy
                .Select(entry => entry.ItemId).Distinct().Count() == business.StockPolicy.Count, $"{business.Id}: invalid stock policy.");
            RequireValidQuantities(business.ProductionTargets, business.Id);
            if (business.SalesPolicy is { } sales)
            {
                Require(sales.SellItems.All(entry => itemIds.Contains(entry.Key)
                    && entry.Value >= 0)
                    && sales.TargetMarkupPercent >= 0
                    && sales.CustomersServedPerWorkerHour >= 0
                    && sales.Channels.All(channel => channel is "retail" or "wholesale"), $"{business.Id}: invalid sales policy.");
            }

            if (business.GridSupplyPolicy is { } grid)
            {
                Require(grid.Utility is "electricity" or "water"
                    && grid.MaxExportKw >= 0
                    && grid.MaxExportPerHour >= 0
                    && grid.UnitPrice >= 0, $"{business.Id}: invalid grid policy.");
            }

            if (business.DisposalPolicy is { } disposal)
            {
                Require(disposal.AcceptedItems.All(itemIds.Contains)
                    && disposal.CapacityTonnes >= 0
                    && disposal.MaxIntakeTonnesPerDay >= 0
                    && disposal.FeePerTonne >= 0, $"{business.Id}: invalid disposal policy.");
            }
        }
    }
}
