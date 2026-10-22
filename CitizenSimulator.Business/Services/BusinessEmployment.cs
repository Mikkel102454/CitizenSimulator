using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Models;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Business.Services;

public partial class BusinessService
{
    private static bool IsServiceRole(string role)
    {
        return role.Contains("cashier", StringComparison.OrdinalIgnoreCase)
            || role.Contains("retail", StringComparison.OrdinalIgnoreCase);
    }

    public bool TryHire(BusinessComponent business, CitizenComponent worker, string role,
        out EmploymentContract? contract, decimal? hourlyWage = null, IReadOnlyList<OpeningHours>? shifts = null)
    {
        contract = null;
        if (!IsRegistered(business) || IsTerminal(business) || !IsLiving(worker))
            return false;

        var requirement = business.Type.Workforce.Find(requirement => requirement.Role == role);
        var account = store.GetComponent<BankAccountComponent>(worker.Parent);
        int skill = store.GetComponent<WorkerProfileComponent>(worker.Parent)?.Skills.GetValueOrDefault(role) ?? 0;
        decimal wage = hourlyWage ?? requirement?.StartingHourlyWage ?? 0;
        if (requirement == null || requirement.DesiredCount <= 0 || skill < requirement.MinimumSkill)
            return false;

        if (account == null || !account.Vault.Accounts.Contains(account) || ReferenceEquals(account, business.PaymentAccount)
            || wage <= 0 || wage < requirement.StartingHourlyWage)
            return false;

        int filledPositions = business.Contracts.Count(contract => contract.IsEmployed && contract.Role == role);
        if (filledPositions >= requirement.DesiredCount || HasActiveJob(worker))
            return false;

        decimal existingHourlyPayroll = business.Contracts
            .Where(contract => contract.IsEmployed).Sum(contract => contract.HourlyWage);
        decimal requiredFunds = business.UnpaidLiabilities + existingHourlyPayroll + wage;

        if (business.PaymentAccount.Balance < requiredFunds)
            return false;

        OpeningHours[] agreedShifts = (shifts ?? business.Type.OpeningHours)
            .Select(openingHours => new OpeningHours
            {
                Days = [.. openingHours.Days],
                Opens = openingHours.Opens,
                Closes = openingHours.Closes
            })
            .ToArray();
        if (agreedShifts.Length == 0 || agreedShifts.Any(openingHours => !BusinessCatalog.ValidHours(openingHours)))
            return false;

        decimal workingMinutes = BusinessTime.WorkingMinutes(agreedShifts, business.Type.OpeningHours,
            Now, Now + TimeSpan.FromDays(8), calender);
        if (workingMinutes <= 0)
            return false;

        contract = new EmploymentContract
        {
            Employee = worker,
            PaymentAccount = account,
            Role = role,
            HourlyWage = wage,
            Shifts = agreedShifts
        };
        business.Contracts.Add(contract);
        return true;
    }

    private bool HasActiveJob(CitizenComponent worker)
    {
        foreach (EntityRef entity in store.Query(BusinessComponent.GetComponentType()))
        {
            BusinessComponent business = store.GetComponent<BusinessComponent>(entity)!;
            bool employsWorker = business.Contracts.Any(contract => contract.IsEmployed
                && contract.Employee.Parent == worker.Parent);
            if (employsWorker)
                return true;
        }

        return false;
    }

    public bool TryDismiss(BusinessComponent business, CitizenComponent worker)
    {
        if (!IsRegistered(business))
            return false;

        var contract = business.Contracts.Find(employment => employment.IsEmployed
            && employment.Employee.Parent == worker.Parent);
        if (contract == null)
            return false;

        RecordAttendance(business, Now);
        contract.IsCheckedIn = false;
        contract.IsAtWorkplace = false;
        contract.IsEmployed = false;
        return true;
    }

    public bool TryCheckIn(BusinessComponent business, CitizenComponent worker)
    {
        if (!IsRegistered(business)
            || !IsLiving(worker)
            || business.State is not (BusinessState.Open or BusinessState.Closed)
            || !BusinessTime.IsOpen(business.Type.OpeningHours, Now, calender))
            return false;

        EmploymentContract? employment = business.Contracts.Find(employment => employment.IsEmployed
                                                                               && employment.Employee.Parent == worker.Parent);
        if (employment == null || employment.IsCheckedIn || !employment.IsAtWorkplace
            || !BusinessTime.IsOpen(employment.Shifts, Now, calender))
            return false;

        WorkShift? shift = GetNextShift(worker);
        if (shift == null || shift.Start > Now)
            return false;

        employment.IsCheckedIn = true;
        employment.CheckedInAt = Now;
        employment.CheckedInUntil = shift.End;
        employment.LastAttendanceProcessedAt = Now;
        return true;
    }

    public bool TryCheckOut(BusinessComponent business, CitizenComponent worker)
    {
        if (!IsRegistered(business))
            return false;

        EmploymentContract? employment = business.Contracts.Find(employment => employment.IsCheckedIn
                                                                               && employment.Employee.Parent == worker.Parent);
        if (employment == null)
            return false;

        RecordAttendance(business, Now);
        employment.IsCheckedIn = false;
        return true;
    }

    private bool HasAttendance(BusinessComponent business)
    {
        foreach (var requirement in business.Type.Workforce.Where(requirement => requirement.DesiredCount > 0))
        {
            bool roleIsCovered = business.Contracts.Any(contract =>
                contract.IsEmployed
                && contract.IsCheckedIn
                && IsLiving(contract.Employee)
                && contract.Role == requirement.Role
                && BusinessTime.IsOpen(contract.Shifts, Now, calender));
            if (!roleIsCovered)
                return false;
        }

        return true;
    }

    private void RecordAttendance(BusinessComponent business, TimeSpan to)
    {
        foreach (var contract in business.Contracts.Where(employment => employment.IsEmployed))
        {
            if (!IsLiving(contract.Employee))
            {
                contract.IsCheckedIn = false;
                contract.IsAtWorkplace = false;
                contract.IsEmployed = false;
                continue;
            }

            if (!contract.IsCheckedIn)
                continue;

            var from = contract.LastAttendanceProcessedAt > contract.CheckedInAt
                ? contract.LastAttendanceProcessedAt
                : contract.CheckedInAt;
            var end = to < contract.CheckedInUntil ? to : contract.CheckedInUntil;
            decimal minutes = BusinessTime.WorkingMinutes(contract.Shifts, business.Type.OpeningHours, from, end, calender);
            contract.LastAttendanceProcessedAt = to;
            contract.MinutesWorked += minutes;
            contract.WagesAccrued += contract.HourlyWage * minutes / 60;
            if (minutes > 0 && to >= contract.CheckedInUntil && contract.CheckedInUntil > contract.LastCompletedShiftEnd)
                contract.LastCompletedShiftEnd = contract.CheckedInUntil;
            if (IsServiceRole(contract.Role))
                business.AvailableServiceMinutes += minutes;
            else
            {
                business.AvailableLaborMinutes += minutes;
            }

            if (to >= contract.CheckedInUntil)
                contract.IsCheckedIn = false;
        }
    }

    public bool TryPayWages(BusinessComponent business)
    {
        if (!IsRegistered(business))
            return false;

        var unpaidContracts = business.Contracts.Where(contract => contract.UnpaidWages > 0).ToArray();
        decimal totalWages = unpaidContracts.Sum(contract => contract.UnpaidWages);
        if (business.PaymentAccount.Balance < totalWages)
            return false;

        foreach (var group in unpaidContracts.GroupBy(contract => contract.PaymentAccount))
        {
            var account = group.Key;
            if (!account.Vault.Accounts.Contains(account))
                return false;

            decimal wagesForAccount = group.Sum(contract => contract.UnpaidWages);
            if (account.Balance > decimal.MaxValue - wagesForAccount)
                return false;
        }

        foreach (var contract in unpaidContracts)
        {
            decimal amount = contract.UnpaidWages;
            if (!vaultService.TransferBalance(business.PaymentAccount, contract.PaymentAccount, amount))
                throw new InvalidOperationException("Prevalidated payroll transfer failed.");

            contract.WagesPaid += amount;
        }

        return true;
    }
}
