using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Planning;
using CitizenSimulator.CitizenMind.Planning.Abstractions;

namespace CitizenSimulator.Business.Models.CitizenMind;

public sealed class JobCandidateProvider(IReadOnlyList<JobVacancy> vacancies) : IActionCandidateProvider
{
    private readonly VacancySnapshot[] _vacancies = vacancies.Select(vacancy => new VacancySnapshot(
        vacancy.Business.Parent, vacancy.Role, vacancy.HourlyWage)).ToArray();

    public IReadOnlyList<ActionCandidate> GetCandidates(ActionDefinition definition, EvaluationContext state)
    {
        return _vacancies.Select(vacancy => new ActionCandidate
        {
            Id = $"{definition.Id}:{vacancy.Employer.Id}:{vacancy.Employer.Generation}:{vacancy.Role}",
            Definition = definition,
            Bindings = new Dictionary<string, object?>
            {
                ["employer"] = vacancy.Employer,
                ["role"] = vacancy.Role,
                ["hourlyWage"] = vacancy.HourlyWage
            }
        }).ToArray();
    }

    private sealed record VacancySnapshot(EntityRef Employer, string Role, decimal HourlyWage);
}
