using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Models;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Property.Components;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Business.Services.Abstractions;

public interface IBusinessService
{
    TimeSpan GameTime { get; }
    IReadOnlyList<Models.Business> GetBusinessTypes();
    IReadOnlyList<Recipe> GetRecipes();
    IReadOnlyList<JobVacancy> GetVacancies();
    IReadOnlyList<JobVacancy> GetAvailableJobs(CitizenComponent worker);
    Employment? GetEmployment(CitizenComponent worker);
    WorkShift? GetNextShift(CitizenComponent worker);
    bool TryResign(CitizenComponent worker);
    bool TryArriveAtWorkplace(BusinessComponent business, CitizenComponent worker);
    bool TryLeaveWorkplace(CitizenComponent worker);
    bool CreateBusiness(ICommandBuffer commands, string typeId, string name,
        CitizenComponent owner, PropertyComponent property, out BusinessComponent? business);
    bool TryActivateBusiness(BusinessComponent business);
    bool TrySuspendBusiness(BusinessComponent business);
    bool TryCloseBusiness(BusinessComponent business);
    bool TryDeclareBankrupt(BusinessComponent business);
    bool TryLiquidateBusiness(ICommandBuffer commands, BusinessComponent business);
    bool TryFundBusiness(BusinessComponent business, decimal amount);
    bool TryWithdrawOwnerFunds(BusinessComponent business, decimal amount);
    bool TrySetOperatingCostRecipient(BusinessComponent business, BankAccountComponent recipient);
    bool TryPayOperatingCosts(BusinessComponent business, BankAccountComponent recipient, decimal amount);
    bool TryStartBatch(BusinessComponent business, string recipeId);
    bool TryAddBatchLabor(BusinessComponent business, decimal laborMinutes);
    bool TryCompleteBatch(BusinessComponent business);
    bool TryCancelBatch(BusinessComponent business);
    bool TryHire(BusinessComponent business, CitizenComponent worker, string role,
        out EmploymentContract? contract, decimal? hourlyWage = null, IReadOnlyList<OpeningHours>? shifts = null);
    bool TryDismiss(BusinessComponent business, CitizenComponent worker);
    bool TryCheckIn(BusinessComponent business, CitizenComponent worker);
    bool TryCheckOut(BusinessComponent business, CitizenComponent worker);
    bool TryPayWages(BusinessComponent business);
    bool TryDepositUtilities(BusinessComponent business, decimal electricityKwh, decimal waterLitres, decimal totalValue = 0);
    bool TryConnectGrid(BusinessComponent business);
    bool TryTransferUtility(BusinessComponent supplier, BusinessComponent receiver, string utility,
        decimal requested, out decimal transferred);
    bool TryDisposeWaste(BusinessComponent source, BusinessComponent disposalBusiness, string itemId, decimal amount);
    decimal GetDisposalCapacity(BusinessComponent disposalBusiness, string itemId);
    void RecordTrade(BusinessComponent seller, BusinessComponent? buyer, decimal price, decimal costOfGoods);
    decimal GetSalePrice(BusinessComponent seller, string itemId);
    bool TryUseCustomerCapacity(BusinessComponent business);
    void AdvanceBusiness(BusinessComponent business, TimeSpan gameTime);
    void BeginTick(ulong tick);
    void ReconcileCreations();
}
