using CitizenSimulator.CitizenMind.Planning;

namespace CitizenSimulator.CitizenMind.Scheduling;

/// <summary>
/// Generates one commitment per simulation day, including overnight windows.
/// Goal conditions and actions are supplied by the owning mod; recurrence does
/// not reset world facts or implement the activity itself.
/// </summary>
public sealed class DailyScheduleRule
{
    public required string Id { get; init; }
    public required string GoalId { get; init; }
    public required DailyTimeWindow Window { get; init; }
    public int Priority { get; init; }
    public TimeSpan PreparationTime { get; init; }

    private IReadOnlyList<DayOfWeek>? _weekdays;

    /// <summary>
    /// Null means every day; an empty list disables the rule. An overnight window
    /// belongs to its start weekday. The supplied list is copied at construction.
    /// </summary>
    public IReadOnlyList<DayOfWeek>? Weekdays
    {
        get => _weekdays;
        init => _weekdays = value?.ToList().AsReadOnly();
    }

    private int WeekdayMask => Weekdays?.Aggregate(0, (mask, day) => mask | (1 << (int)day)) ?? 127;

    internal bool RunsOn(DayOfWeek day) => (WeekdayMask & (1 << (int)day)) != 0;

    internal bool Matches(DailyScheduleRule other)
    {
        return Id == other.Id && GoalId == other.GoalId &&
               Window.Start == other.Window.Start && Window.End == other.Window.End &&
               Priority == other.Priority && PreparationTime == other.PreparationTime &&
               WeekdayMask == other.WeekdayMask;
    }
}
