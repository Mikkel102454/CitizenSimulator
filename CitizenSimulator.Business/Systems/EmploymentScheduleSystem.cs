using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Business.Models.CitizenMind;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.Calender.Services.Abstractions;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.CitizenMind.Components;
using CitizenSimulator.CitizenMind.Planning;
using CitizenSimulator.CitizenMind.Scheduling;
using CitizenSimulator.CitizenMind.Services.Abstractions;

namespace CitizenSimulator.Business.Systems;

public sealed class EmploymentScheduleSystem(IBusinessService businesses, ICitizenMindService minds,
    ICalenderService calendar) : GameSystem(priority: -2)
{
    public const string ScheduleSource = "business.employment";

    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {
        foreach (EntityRef entity in Store.Query(MindComponent.GetComponentType()).OrderBy(entity => entity.Id))
        {
            var worker = Store.GetComponent<CitizenComponent>(entity);
            var commitments = new List<ScheduledCommitment>();
            TimeSpan now = businesses.GameTime;
            // Keep a bounded history so completed/missed occurrences remain inspectable.
            commitments.AddRange(minds.GetSchedule(entity).Where(entry => entry.SourceId == ScheduleSource
                && entry.Commitment.Window.End <= now && entry.Commitment.Window.End >= now - TimeSpan.FromDays(7))
                .Select(entry => entry.Commitment));
            if (worker is { Alive: true })
            {
                TimeSpan preparation = TimeSpan.FromMinutes(EmploymentActions.GetCommuteMinutes(Store, entity))
                    + calendar.GetGameTime(1);
                foreach (var shift in businesses.GetUpcomingShifts(worker))
                {
                    commitments.Add(new ScheduledCommitment
                    {
                        Id = $"business.shift:{shift.Employment.Contract.Id:N}:{shift.End.Ticks}",
                        GoalId = EmploymentActions.AttendShiftGoal,
                        Window = new TimeWindow(shift.Start, shift.End),
                        PreparationTime = preparation,
                        Priority = 40,
                        Target = new CommitmentTarget(EmploymentActions.CompletedShiftFact, shift.End.TotalMinutes)
                    });
                }
            }

            minds.SetScheduleSource(entity, ScheduleSource, commitments);
        }
    }
}
