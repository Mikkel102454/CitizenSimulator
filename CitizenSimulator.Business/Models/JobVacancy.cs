using CitizenSimulator.Business.Components;

namespace CitizenSimulator.Business.Models;

public sealed record JobVacancy(BusinessComponent Business, string Role,
    int AvailablePositions, int MinimumSkill, decimal HourlyWage);
