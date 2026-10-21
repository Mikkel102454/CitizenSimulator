using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Models;

namespace CitizenSimulator.CitizenMind.Planning.Abstractions;

public interface IActionCandidateProvider
{
    IReadOnlyList<ActionCandidate> GetCandidates(ActionDefinition definition, EvaluationContext state);
}