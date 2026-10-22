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

    internal bool Matches(DailyScheduleRule other)
        => Id == other.Id && GoalId == other.GoalId &&
           Window.Start == other.Window.Start && Window.End == other.Window.End &&
           Priority == other.Priority && PreparationTime == other.PreparationTime;
}
