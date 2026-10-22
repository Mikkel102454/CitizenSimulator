using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Planning;
using CitizenSimulator.CitizenMind.Planning.Abstractions;

namespace CitizenSimulator.Business.Models.CitizenMind;

public sealed class ShiftCandidateProvider(WorkShift? shift) : IActionCandidateProvider
{
    public IReadOnlyList<ActionCandidate> GetCandidates(ActionDefinition definition, EvaluationContext state)
    {
        if (shift == null || state.GameTime >= shift.End)
            return [];

        var start = definition.Id == EmploymentActions.WorkAction ? shift.Start : state.GameTime;
        return
        [
            new ActionCandidate
            {
                Id = $"{definition.Id}:{shift.Employment.Contract.Id}:{shift.End.Ticks}",
                Definition = definition,
                Bindings = new Dictionary<string, object?>
                {
                    ["employer"] = shift.Employment.Business.Parent,
                    ["contractId"] = shift.Employment.Contract.Id,
                    ["shiftEndMinutes"] = shift.End.TotalMinutes
                },
                Availability = [new TimeWindow(start, shift.End)]
            }
        ];
    }
}
