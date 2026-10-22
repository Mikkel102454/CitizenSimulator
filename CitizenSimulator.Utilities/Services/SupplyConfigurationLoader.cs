using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.Items.Services.Abstractions;
using CitizenSimulator.Utilities.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CitizenSimulator.Utilities.Services;

public static class SupplyConfigurationLoader
{
    public static SupplyConfiguration Load(string path, IItemService items, IBusinessService businesses)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithDuplicateKeyChecking()
            .Build();
        using var reader = File.OpenText(path);
        SupplyConfiguration configuration = deserializer.Deserialize<SupplyConfiguration>(reader)
            ?? throw new InvalidOperationException($"Empty supply configuration: {path}");
        Validate(configuration, items, businesses);
        return configuration;
    }

    public static void Validate(SupplyConfiguration configuration, IItemService items, IBusinessService businesses)
    {
        void Require(bool valid, string message)
        {
            if (!valid)
                throw new InvalidOperationException($"World supply: {message}");
        }

        Require(configuration.ConnectionElectricityKw >= 0 && configuration.ConnectionWaterLitresPerHour >= 0
            && configuration.CollectionTonnesPerDay >= 0, "negative connection or collection capacity.");
        Require(configuration.Startup.FundBalance >= 0 && configuration.Startup.WorkingCapitalDays >= 0,
            "negative startup policy.");
        HouseholdPolicy households = configuration.Households;
        Require(households.ElectricityKw >= 0 && households.WaterLitresPerHour >= 0
            && households.ElectricityKwhPerResidentPerDay >= 0 && households.WaterLitresPerResidentPerDay >= 0
            && households.BufferHours >= 0, "negative household utility policy.");
        var businessIds = businesses.GetBusinessTypes().Select(business => business.Id).ToHashSet();
        Require(configuration.Startup.WorkingCapitalDaysByBusiness.All(entry => businessIds.Contains(entry.Key)
            && entry.Value >= 0), "unknown business or negative working capital days.");

        var catalog = items.GetItems().ToDictionary(item => item.Id);
        foreach (var (itemId, policy) in configuration.Items)
        {
            Require(catalog.TryGetValue(itemId, out var item), $"unknown import item '{itemId}'.");
            Require(!itemId.StartsWith("utility.", StringComparison.Ordinal), $"'{itemId}' belongs in utilities, not item imports.");
            Require(policy.Unit == item!.Unit, $"'{itemId}' uses '{item.Unit}', not '{policy.Unit}'.");
            Require(policy.UnitPrice >= 0 && policy.MaxPerDay >= 0, $"invalid import price/capacity for '{itemId}'.");
        }

        foreach (var (utility, policy) in configuration.Utilities)
        {
            string expectedUnit = utility == "electricity" ? "kWh" : "litre";
            Require(utility is "electricity" or "water", $"unknown utility '{utility}'.");
            Require(policy.Unit == expectedUnit && policy.UnitPrice >= 0 && policy.MaxPerHour >= 0,
                $"invalid unit, price or capacity for '{utility}'.");
        }

        foreach (var (itemId, policy) in configuration.Waste)
        {
            Require(catalog.TryGetValue(itemId, out var item) && itemId.StartsWith("waste.", StringComparison.Ordinal)
                && item.MassKgPerUnit > 0, $"invalid waste item '{itemId}'.");
            Require(policy.FeePerTonne >= 0 && policy.MaxTonnesPerDay >= 0 && policy.CapacityTonnes >= 0,
                $"invalid waste policy for '{itemId}'.");
        }
    }
}
