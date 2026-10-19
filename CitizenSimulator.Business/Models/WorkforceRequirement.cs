namespace CitizenSimulator.Business.Models;

public class WorkforceRequirement
{
    public string Role { get; init; } = "";
    public int DesiredCount { get; init; }
    public int MinimumSkill { get; init; }
    public decimal StartingHourlyWage { get; init; }
}
