using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Calender.Services.Abstractions;

namespace CitizenSimulator.Calender.Services;

public class CalenderService : ICalenderService
{
    internal ulong TickOfDay;
    public ulong GetTicksPerDay()
    {
        return 24 * 60; // each tick is 1 minute
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
}