namespace CitizenSimulator.Calender.Services.Abstractions;

public interface ICalenderService
{
    ulong GetTicksPerDay();
    string GetTimeOfDay();
    
    TimeSpan GetGameTime(ulong tick);

    /// <summary>Gets the date at an absolute, nonnegative game time.</summary>
    DateOnly GetDate(TimeSpan gameTime);

    /// <summary>Uses the same simulation epoch as the live calendar.</summary>
    DayOfWeek GetDayOfWeek(TimeSpan gameTime);
}
