using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Planning;
using CitizenSimulator.CitizenMind.Planning.Abstractions;

namespace CitizenSimulator.Business.Models.CitizenMind;

public sealed class JobCandidateProvider(IReadOnlyList<JobVacancy> vacancies) : IActionCandidateProvider
{
    public IReadOnlyList<ActionCandidate> GetCandidates(ActionDefinition definition, EvaluationContext state)
    {
        return vacancies.Select(vacancy => new ActionCandidate
        {
            Id = $"{definition.Id}:{vacancy.Business.Parent.Id}:{vacancy.Business.Parent.Generation}:{vacancy.Role}",
            Definition = definition,
            Bindings = new Dictionary<string, object?>
            {
                ["employer"] = vacancy.Business.Parent,
                ["role"] = vacancy.Role,
                ["hourlyWage"] = vacancy.HourlyWage
            }
        }).ToArray();
    }
}
