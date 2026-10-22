using System.Collections.ObjectModel;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Models;

namespace CitizenSimulator.Business.Services;

public partial class BusinessService
{
    public bool TryStartBatch(BusinessComponent business, string recipeId)
    {
        if (!IsRegistered(business)
            || !business.IsOperating
            || business.ActiveBatch != null
            || !HasValidPremises(business)
            || !business.Type.Recipes.Contains(recipeId))
            return false;

        var recipe = Recipe.RecipeList.Find(definition => definition.Id == recipeId);
        if (recipe == null)
            return false;

        if (!HasAttendance(business))
        {
            business.ProductionIssue = OperationalIssue.MissingStaff;
            return false;
        }

        if (!HasRequiredEquipment(business))
        {
            business.ProductionIssue = OperationalIssue.MissingEquipment;
            return false;
        }

        if (!CanConsumeUtilities(business, recipe.UtilitiesPerBatch.ElectricityKwh, recipe.UtilitiesPerBatch.WaterLitres))
        {
            business.ProductionIssue = OperationalIssue.MissingUtilities;
            return false;
        }

        var equipment = GetRequiredEquipment(business.Type.Equipment);
        foreach (var (itemId, quantity) in GetRequiredEquipment(recipe.RequiredEquipment))
        {
            equipment[itemId] = Math.Max(equipment.GetValueOrDefault(itemId), quantity);
        }

        var reserved = new Dictionary<string, decimal>(recipe.Inputs);
        foreach (var (itemId, quantity) in equipment)
        {
            reserved[itemId] = reserved.GetValueOrDefault(itemId) + quantity;
        }

        if (!inventoryService.TryReserve(business.Inventory, reserved, out var reservation))
        {
            business.ProductionIssue = OperationalIssue.MissingInputs;
            return false;
        }

        decimal materialValue = 0;
        foreach (var (itemId, inputQuantity) in recipe.Inputs)
        {
            decimal reservedValue = reservation!.Values.GetValueOrDefault(itemId);
            decimal materialShare = inputQuantity / reservation.Quantities[itemId];
            materialValue += reservedValue * materialShare;
        }

        var batch = new ProductionBatch
        {
            Recipe = recipe,
            Reservation = reservation!,
            ReservedInputs = new ReadOnlyDictionary<string, decimal>(new Dictionary<string, decimal>(recipe.Inputs)),
            ReservedEquipment = new ReadOnlyDictionary<string, decimal>(equipment),
            MaterialValue = materialValue
        };
        ConsumeUtilities(business, recipe.UtilitiesPerBatch.ElectricityKwh, recipe.UtilitiesPerBatch.WaterLitres);
        business.ActiveBatch = batch;
        business.ProductionIssue = OperationalIssue.None;
        return true;
    }

    public bool TryAddBatchLabor(BusinessComponent business, decimal laborMinutes)
    {
        if (laborMinutes <= 0
            || !IsRegistered(business)
            || IsTerminal(business)
            || business.State == BusinessState.Suspended
            || business.ActiveBatch is not { } batch
            || business.AvailableLaborMinutes <= 0)
            return false;

        decimal remainingLabor = Math.Max(0, batch.Recipe.LaborMinutes - batch.LaborMinutesWorked);
        decimal availableLabor = Math.Min(laborMinutes, business.AvailableLaborMinutes);
        decimal accepted = Math.Min(availableLabor, remainingLabor);
        if (accepted == 0)
            return false;

        batch.LaborMinutesWorked += accepted;
        business.AvailableLaborMinutes -= accepted;
        return true;
    }

    public bool TryCancelBatch(BusinessComponent business)
    {
        if (!IsRegistered(business)
            || business.ActiveBatch is not { } batch
            || !inventoryService.TryRelease(business.Inventory, batch.Reservation))
            return false;

        business.ActiveBatch = null;
        return true;
    }

    public bool TryCompleteBatch(BusinessComponent business)
    {
        if (!IsRegistered(business)
            || business.State is not (BusinessState.Open or BusinessState.Closed)
            || business.Property.Destroyed
            || business.ActiveBatch is not { IsReadyToComplete: true } batch)
            return false;

        var outputs = new Dictionary<string, decimal>(batch.ReservedEquipment);
        var values = new Dictionary<string, decimal>();
        foreach (var (itemId, equipmentQuantity) in batch.ReservedEquipment)
        {
            decimal reservedValue = batch.Reservation.Values.GetValueOrDefault(itemId);
            decimal equipmentShare = equipmentQuantity / batch.Reservation.Quantities[itemId];
            values[itemId] = reservedValue * equipmentShare;
        }

        foreach (var output in batch.Recipe.Outputs.Concat(batch.Recipe.WasteOutputs))
        {
            outputs[output.Key] = outputs.GetValueOrDefault(output.Key) + output.Value;
        }

        decimal productQuantity = batch.Recipe.Outputs.Values.Sum();
        decimal assignedValue = 0;
        var products = batch.Recipe.Outputs.ToArray();
        for (int i = 0; i < products.Length; i++)
        {
            var productEntry = products[i];
            // Give the last product any rounding remainder so the total book value stays unchanged.
            decimal share = i == products.Length - 1
                ? batch.MaterialValue - assignedValue
                : batch.MaterialValue * (productEntry.Value / productQuantity);
            values[productEntry.Key] = values.GetValueOrDefault(productEntry.Key) + share;
            assignedValue += share;
        }

        // Compute all scalar updates before exchanging the inventory reservation.
        decimal electricityAfter = business.ElectricityKwh + batch.Recipe.UtilityOutputsPerBatch.ElectricityKwh;
        decimal waterAfter = business.WaterLitres + batch.Recipe.UtilityOutputsPerBatch.WaterLitres;
        decimal lossesAfter = business.ProductionLosses + (productQuantity == 0 ? batch.MaterialValue : 0);
        if (!inventoryService.TryExchange(business.Inventory, batch.Reservation, outputs, values))
        {
            business.ProductionIssue = OperationalIssue.OutputStorageFull;
            return false;
        }

        business.ElectricityKwh = electricityAfter;
        business.WaterLitres = waterAfter;
        business.ProductionLosses = lossesAfter;
        business.ActiveBatch = null;
        business.ProductionIssue = OperationalIssue.None;
        return true;
    }

    private bool WantsProduction(BusinessComponent business, Recipe recipe)
    {
        foreach (var (itemId, batchQuantity) in recipe.Outputs)
        {
            decimal target = business.Type.ProductionTargets.GetValueOrDefault(itemId, batchQuantity * 2);
            target = business.GetStockTarget(itemId, target);
            if (business.Inventory.GetQuantity(itemId) < target)
                return true;
        }

        var utilityOutputs = recipe.UtilityOutputsPerBatch;
        decimal electricityTarget = Math.Max(utilityOutputs.ElectricityKwh * 2,
            business.Type.Utilities.ElectricityKwhPerOperatingHour * business.UtilityBufferHours);
        decimal waterTarget = Math.Max(utilityOutputs.WaterLitres * 2,
            business.Type.Utilities.WaterLitresPerOperatingHour * business.UtilityBufferHours);
        bool needsElectricity = utilityOutputs.ElectricityKwh > 0 && business.ElectricityKwh < electricityTarget;
        bool needsWater = utilityOutputs.WaterLitres > 0 && business.WaterLitres < waterTarget;
        return needsElectricity || needsWater;
    }
}
