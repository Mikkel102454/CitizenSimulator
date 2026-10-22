using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Business.Models;

public sealed class EmploymentContract
{
    public Guid Id { get; } = Guid.NewGuid();
    public required CitizenComponent Employee { get; init; }
    public required BankAccountComponent PaymentAccount { get; init; }
    public required string Role { get; init; }
    public required decimal HourlyWage { get; init; }
    public required IReadOnlyList<OpeningHours> Shifts { get; init; }
    public bool IsEmployed { get; internal set; } = true;
    public bool IsCheckedIn { get; internal set; }
    public TimeSpan CheckedInAt { get; internal set; }
    public TimeSpan CheckedInUntil { get; internal set; }
    public TimeSpan LastAttendanceProcessedAt { get; internal set; }
    public decimal MinutesWorked { get; internal set; }
    public decimal WagesAccrued { get; internal set; }
    public decimal WagesPaid { get; internal set; }
    public decimal UnpaidWages => WagesAccrued - WagesPaid;
}
