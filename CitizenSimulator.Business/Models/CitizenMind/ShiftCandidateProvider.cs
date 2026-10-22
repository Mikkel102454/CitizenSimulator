using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Planning;
using CitizenSimulator.CitizenMind.Planning.Abstractions;

namespace CitizenSimulator.Business.Models.CitizenMind;

public sealed class ShiftCandidateProvider(IReadOnlyList<WorkShift> shifts, double commuteMinutes,
    TimeSpan actionBoundary) : IActionCandidateProvider
{
    private readonly ShiftSnapshot[] _shifts = shifts.Select(shift => new ShiftSnapshot(
        shift.Employment.Business.Parent, shift.Employment.Contract.Id, shift.Start, shift.End)).ToArray();

    public IReadOnlyList<ActionCandidate> GetCandidates(ActionDefinition definition, EvaluationContext state)
    {
        double completed = Convert.ToDouble(state.Facts.GetValueOrDefault(EmploymentActions.CompletedShiftFact) ?? 0);
        var candidates = new List<ActionCandidate>();
        foreach (ShiftSnapshot shift in _shifts)
        {
            if (state.GameTime >= shift.End || completed >= shift.End.TotalMinutes)
                continue;

            TimeSpan start = definition.Id == EmploymentActions.WorkAction ? shift.Start
                : shift.Start - TimeSpan.FromMinutes(commuteMinutes) - actionBoundary;
            start = start < TimeSpan.Zero ? TimeSpan.Zero : start;
            candidates.Add(new ActionCandidate
            {
                Id = $"{definition.Id}:{shift.ContractId}:{shift.Start.Ticks}:{shift.End.Ticks}",
                Definition = definition,
                Bindings = new Dictionary<string, object?>
                {
                    ["employer"] = shift.Employer,
                    ["contractId"] = shift.ContractId,
                    ["shiftStartMinutes"] = shift.Start.TotalMinutes,
                    ["shiftEndMinutes"] = shift.End.TotalMinutes
                },
                Availability = [new TimeWindow(start, shift.End)]
            });
        }

        return candidates;
    }

    private sealed record ShiftSnapshot(EntityRef Employer, Guid ContractId, TimeSpan Start, TimeSpan End);
}
