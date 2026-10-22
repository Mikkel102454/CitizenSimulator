using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Calender.Services.Abstractions;

namespace CitizenSimulator.Calender.Services;

public class CalenderService : ICalenderService
{
    private static readonly DateOnly StartDate = new(2000, 1, 1);

    internal ulong TickOfDay;
    public ulong GetTicksPerDay()
    {
        return (24 * 60 / 5); // each tick is 5 minute
    }

    public ulong GetTickOfDay()
    {
        return TickOfDay; // 10 seconds
    }

    public string GetTimeOfDay()
    {
        ulong ticksPerDay = GetTicksPerDay();
        int hour = (int)(TickOfDay * 24 / ticksPerDay);
        int minute = (int)(TickOfDay * 1440 / ticksPerDay % 60);

        return $"{hour}:{minute}";
    }
    
    public TimeSpan GetGameTime(ulong tick)
    {
        decimal timeSpanTicks = (decimal)tick * TimeSpan.TicksPerDay / GetTicksPerDay();

        return TimeSpan.FromTicks((long)timeSpanTicks);
    }

    public DateOnly GetDate(TimeSpan gameTime)
    {
        if (gameTime < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(gameTime));

        int days = checked((int)(gameTime.Ticks / TimeSpan.TicksPerDay));
        return StartDate.AddDays(days);
    }

    public DayOfWeek GetDayOfWeek(TimeSpan gameTime) => GetDate(gameTime).DayOfWeek;
}
