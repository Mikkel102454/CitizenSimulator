using CitizenSimulator.CitizenMind.Components;
using CitizenSimulator.Calender.Services.Abstractions;

namespace CitizenSimulator.CitizenMind.Scheduling;

internal static class DailyScheduleGenerator
{
    internal const string OccurrenceIdPrefix = "citizen_mind.daily:";

    public static void Refresh(MindComponent mind, TimeSpan gameTime, TimeSpan planningHorizon, ICalenderService calender)
    {
        if (mind.DailyScheduleRules.Count == 0)
            return;

        var horizonEnd = gameTime + planningHorizon;
        long firstDay = Math.Max(0, gameTime.Ticks / TimeSpan.TicksPerDay - 1);
        long lastDay = horizonEnd.Ticks / TimeSpan.TicksPerDay;
        var existingIds = mind.Schedule.Select(entry => entry.Commitment.Id).ToHashSet();
        var additions = new List<ScheduledCommitmentState>();

        foreach (var rule in mind.DailyScheduleRules)
        {
            for (long day = firstDay; day <= lastDay; day++)
            {
                var window = rule.Window.GetWindowForDay(day);
                if (window.End < gameTime || window.Start > horizonEnd)
                    continue;

                if (!rule.RunsOn(calender.GetDayOfWeek(window.Start)))
                    continue;

                // Length-prefixing the rule ID keeps occurrence IDs unambiguous.
                string id = FormattableString.Invariant($"{OccurrenceIdPrefix}{rule.Id.Length}:{rule.Id}:{day}");
                if (!existingIds.Add(id))
                    continue;

                additions.Add(new ScheduledCommitmentState(new ScheduledCommitment
                {
                    Id = id,
                    GoalId = rule.GoalId,
                    Window = window,
                    Priority = rule.Priority,
                    PreparationTime = rule.PreparationTime
                }, rule.Id, day));
            }
        }

        if (additions.Count == 0)
            return;

        long revision = checked(mind.ScheduleRevision + 1);
        mind.Schedule.AddRange(additions);
        mind.ScheduleRevision = revision;

        foreach (ScheduledCommitmentState entry in additions)
            mind.GoalRetryTicks.Remove(entry.Commitment.GoalId);
    }
}
