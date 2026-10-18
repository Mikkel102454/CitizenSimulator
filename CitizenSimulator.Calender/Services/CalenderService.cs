using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Calender.Services.Abstractions;

namespace CitizenSimulator.Calender.Services;

public class CalenderService(IEntityStore store, IEventBus events) : ICalenderService
{
    public ulong GetTicksPerDay()
    {
        return 30 * 10; // 10 seconds
    }
}