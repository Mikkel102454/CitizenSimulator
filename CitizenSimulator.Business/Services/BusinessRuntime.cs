using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Models;

namespace CitizenSimulator.Business.Services;

public partial class BusinessService
{
    public void AdvanceBusiness(BusinessComponent business, TimeSpan gameTime)
    {
        if (!IsRegistered(business) || IsTerminal(business) || business.State == BusinessState.SettingUp)
            return;

        var previousUpdate = business.LastUpdatedAt ?? gameTime;
        if (gameTime <= previousUpdate)
            return;

        business.LastUpdatedAt = gameTime;
        business.AvailableLaborMinutes = 0;
        business.AvailableServiceMinutes = 0;
        if (business.State == BusinessState.Suspended)
        {
            CheckBankruptcyDeadline(business, gameTime);
            return;
        }

        if (!IsLiving(business.Owner))
        {
            business.Issue = OperationalIssue.OwnerUnavailable;
            TrySuspendBusiness(business);
            return;
        }

        if (!HasValidPremises(business))
        {
            business.Issue = OperationalIssue.InvalidPremises;
            TrySuspendBusiness(business);
            return;
        }

        bool isOpen = BusinessTime.IsOpen(business.Type.OpeningHours, gameTime, calender);
        business.State = isOpen ? BusinessState.Open : BusinessState.Closed;
        business.Issue = OperationalIssue.None;
        decimal elapsedMinutes = (gameTime - previousUpdate).Ticks / (decimal)TimeSpan.TicksPerMinute;
        AccrueOperatingCosts(business, elapsedMinutes);
        RecordAttendance(business, gameTime);
        if (!TrySettleBusinessBills(business))
        {
            business.Issue = OperationalIssue.InsufficientFunds;
            business.InsolventSince ??= gameTime;
            TrySuspendBusiness(business);
            return;
        }

        business.InsolventSince = null;
        UpdateOperatingResources(business, previousUpdate, gameTime);
        AdvanceActiveBatch(business, elapsedMinutes);
        if (business.IsOperating && business.ActiveBatch == null)
            TryStartNextBatch(business);
    }

    private void CheckBankruptcyDeadline(BusinessComponent business, TimeSpan gameTime)
    {
        if (business.InsolventSince is not { } insolventSince)
            return;

        var gracePeriod = TimeSpan.FromDays((double)business.Type.BankruptcyGraceDays);
        if (gameTime - insolventSince >= gracePeriod)
            TryDeclareBankrupt(business);
    }

    private static void AccrueOperatingCosts(BusinessComponent business, decimal elapsedMinutes)
    {
        const decimal minutesPerDay = 1440;
        var costs = business.Type.OperatingCosts;
        decimal dailyCost = costs.InsurancePerDay + costs.MaintenanceBudgetPerDay;
        decimal accruedCost = dailyCost * elapsedMinutes / minutesPerDay;
        business.OperatingCostsAccrued += accruedCost;
        business.UnpaidOperatingCosts += accruedCost;
    }

    private bool TrySettleBusinessBills(BusinessComponent business)
    {
        if (!TryPayWages(business))
            return false;

        if (business.UnpaidOperatingCosts <= 0)
            return true;

        var recipient = business.OperatingCostRecipient;
        return recipient != null && TryPayOperatingCosts(business, recipient, business.UnpaidOperatingCosts);
    }

    private void UpdateOperatingResources(BusinessComponent business, TimeSpan from, TimeSpan to)
    {
        decimal openMinutes = BusinessTime.OverlapMinutes(business.Type.OpeningHours, from, to, calender);
        decimal electricityNeeded = business.Type.Utilities.ElectricityKwhPerOperatingHour * openMinutes / 60;
        decimal waterNeeded = business.Type.Utilities.WaterLitresPerOperatingHour * openMinutes / 60;
        bool hasStaff = business.State == BusinessState.Closed || HasAttendance(business);
        bool hasEquipment = HasRequiredEquipment(business);
        bool hasUtilities = CanConsumeUtilities(business, electricityNeeded, waterNeeded);
        if (!hasStaff)
            business.Issue = OperationalIssue.MissingStaff;
        else if (!hasEquipment)
            business.Issue = OperationalIssue.MissingEquipment;
        else if (!hasUtilities)
            business.Issue = OperationalIssue.MissingUtilities;

        if (!hasStaff || !hasEquipment || !hasUtilities)
        {
            business.AvailableLaborMinutes = 0;
            return;
        }

        ConsumeUtilities(business, electricityNeeded, waterNeeded);
        UpdateCustomerCredits(business);
    }

    private static void UpdateCustomerCredits(BusinessComponent business)
    {
        bool hasServiceRole = business.Type.Workforce.Any(role => IsServiceRole(role.Role));
        decimal servingMinutes = hasServiceRole ? business.AvailableServiceMinutes : business.AvailableLaborMinutes;
        decimal customersPerHour = business.Type.SalesPolicy?.CustomersServedPerWorkerHour ?? 0;
        int employeeCount = business.Contracts.Count(contract => contract.IsEmployed);
        decimal creditLimit = Math.Max(1, customersPerHour * Math.Max(1, employeeCount));
        decimal earnedCredits = servingMinutes * customersPerHour / 60;
        business.CustomerCredits = Math.Min(creditLimit, business.CustomerCredits + earnedCredits);
    }

    private void AdvanceActiveBatch(BusinessComponent business, decimal elapsedMinutes)
    {
        if (business.ActiveBatch is not { } batch)
            return;

        decimal remainingMinutes = Math.Max(0, batch.Recipe.DurationMinutes - batch.ElapsedMinutes);
        batch.ElapsedMinutes += Math.Min(elapsedMinutes, remainingMinutes);
        TryAddBatchLabor(business, business.AvailableLaborMinutes);
        if (batch.IsReadyToComplete)
            TryCompleteBatch(business);
    }

    private void TryStartNextBatch(BusinessComponent business)
    {
        foreach (var recipeId in business.Type.Recipes)
        {
            var recipe = Recipe.RecipeList.Single(recipe => recipe.Id == recipeId);
            if (WantsProduction(business, recipe) && TryStartBatch(business, recipeId))
                break;

            // A shortage in one recipe must not hide another feasible route.
            if (business.Issue is OperationalIssue.MissingInputs or OperationalIssue.MissingUtilities)
                business.Issue = OperationalIssue.None;
        }
    }
}
