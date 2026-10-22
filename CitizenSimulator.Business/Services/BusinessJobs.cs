using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Models;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Business.Services;

public partial class BusinessService
{
    public IReadOnlyList<JobVacancy> GetVacancies()
    {
        var vacancies = new List<JobVacancy>();
        var businesses = store.Query(BusinessComponent.GetComponentType())
            .OrderBy(entity => entity.Id)
            .Select(entity => store.GetComponent<BusinessComponent>(entity)!);

        foreach (var business in businesses)
        {
            if (IsTerminal(business) || !IsLiving(business.Owner) || !HasValidPremises(business)
                || !business.PaymentAccount.Vault.Accounts.Contains(business.PaymentAccount)
                || (business.State == BusinessState.Suspended && business.UnpaidWages > 0))
                continue;

            decimal hourlyPayroll = business.Contracts
                .Where(contract => contract.IsEmployed)
                .Sum(contract => contract.HourlyWage);

            foreach (var requirement in business.Type.Workforce)
            {
                int employed = business.Contracts.Count(contract =>
                    contract.IsEmployed && contract.Role == requirement.Role);
                int availablePositions = requirement.DesiredCount - employed;
                decimal requiredFunds = business.UnpaidLiabilities + hourlyPayroll + requirement.StartingHourlyWage;
                if (availablePositions <= 0 || business.PaymentAccount.Balance < requiredFunds)
                    continue;

                vacancies.Add(new JobVacancy(business, requirement.Role, availablePositions,
                    requirement.MinimumSkill, requirement.StartingHourlyWage));
            }
        }

        return vacancies;
    }

    public IReadOnlyList<JobVacancy> GetAvailableJobs(CitizenComponent worker)
    {
        if (!IsLiving(worker) || GetEmployment(worker) != null)
            return [];

        var account = store.GetComponent<BankAccountComponent>(worker.Parent);
        if (account == null || !account.Vault.Accounts.Contains(account))
            return [];

        var profile = store.GetComponent<WorkerProfileComponent>(worker.Parent);
        return GetVacancies()
            .Where(vacancy => !ReferenceEquals(vacancy.Business.PaymentAccount, account)
                && (profile?.Skills.GetValueOrDefault(vacancy.Role) ?? 0) >= vacancy.MinimumSkill)
            .OrderByDescending(vacancy => vacancy.HourlyWage)
            .ThenBy(vacancy => vacancy.Business.Parent.Id)
            .ThenBy(vacancy => vacancy.Role, StringComparer.Ordinal)
            .ToArray();
    }

    public Employment? GetEmployment(CitizenComponent worker)
    {
        if (!IsLiving(worker))
            return null;

        foreach (EntityRef entity in store.Query(BusinessComponent.GetComponentType()).OrderBy(entity => entity.Id))
        {
            BusinessComponent business = store.GetComponent<BusinessComponent>(entity)!;
            EmploymentContract? contract = business.Contracts.Find(contract =>
                contract.IsEmployed && contract.Employee.Parent == worker.Parent);
            if (contract != null)
                return new Employment(business, contract);
        }

        return null;
    }

    public WorkShift? GetNextShift(CitizenComponent worker)
    {
        Employment? employment = GetEmployment(worker);
        if (employment == null || !HasValidPremises(employment.Business)
            || employment.Business.State is not (BusinessState.Open or BusinessState.Closed))
            return null;

        // Eight days includes the next occurrence of every weekday, plus overnight tails.
        IReadOnlyList<(TimeSpan Start, TimeSpan End)> windows = BusinessTime.GetWorkingWindows(employment.Contract.Shifts,
            employment.Business.Type.OpeningHours, Now, Now + TimeSpan.FromDays(8), calender);
        if (windows.Count == 0)
            return null;

        (TimeSpan Start, TimeSpan End) nextWindow = windows[0];
        return new WorkShift(employment, nextWindow.Start, nextWindow.End);
    }

    public bool TryResign(CitizenComponent worker)
    {
        Employment? employment = GetEmployment(worker);
        if (employment == null)
            return false;

        return TryDismiss(employment.Business, worker);
    }

    public IReadOnlyList<WorkShift> GetUpcomingShifts(CitizenComponent worker, int days = 8)
    {
        Employment? employment = GetEmployment(worker);
        if (days is < 1 or > 31 || employment == null || !HasValidPremises(employment.Business)
            || employment.Business.State is not (BusinessState.Open or BusinessState.Closed))
            return [];

        long today = Now.Ticks / TimeSpan.TicksPerDay;
        TimeSpan from = TimeSpan.FromTicks(Math.Max(0, today - 1) * TimeSpan.TicksPerDay);
        TimeSpan until = TimeSpan.FromTicks((today + days + 2) * TimeSpan.TicksPerDay);
        return BusinessTime.GetWorkingWindows(employment.Contract.Shifts, employment.Business.Type.OpeningHours,
                from, until, calender)
            .Where(window => window.End > Now && window.Start <= Now + TimeSpan.FromDays(days))
            .Select(window => new WorkShift(employment, window.Start, window.End)).ToArray();
    }

    public bool TryArriveAtWorkplace(BusinessComponent business, CitizenComponent worker)
    {
        Employment? employment = GetEmployment(worker);
        if (employment == null || !ReferenceEquals(employment.Business, business)
            || !HasValidPremises(business) || IsTerminal(business))
            return false;

        employment.Contract.IsAtWorkplace = true;
        return true;
    }

    public bool TryLeaveWorkplace(CitizenComponent worker)
    {
        Employment? employment = GetEmployment(worker);
        if (employment == null)
            return false;

        if (employment.Contract.IsCheckedIn)
            TryCheckOut(employment.Business, worker);

        employment.Contract.IsAtWorkplace = false;
        return true;
    }
}
