using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Calender.Components;
using CitizenSimulator.Calender.Events;

namespace CitizenSimulator.Calender.Systems;

public class CalenderSystem(IEventBus events) : GameSystem
{
    private const ulong TICKS_PER_DAY = 30 * 10; // 10 seconds
    private const int STARTYEAR = 2000; // 10 seconds
    private CalenderComponent _calenderComponent;

    protected override void OnStart(ICommandBuffer commands)
    {
        EntityRef entity = commands.CreateEntity();
        _calenderComponent = commands.AddComponent(entity, new CalenderComponent());
        
        _calenderComponent.DayOfWeek = (STARTYEAR + STARTYEAR / 4 - STARTYEAR / 100 + STARTYEAR / 400) % 7;
        _calenderComponent.DayOfMonth = 1;
        _calenderComponent.MonthOfYear = CalenderComponent.Months.JANUARY;
        _calenderComponent.Year = STARTYEAR;
        
        events.Publish(new OnDayStart());
        events.Publish(new OnMonthStart());
        events.Publish(new OnYearStart());
        events.Publish(new OnWeekStart());
    }

    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {
        ulong tickOfDay = time.Tick % TICKS_PER_DAY;
        
        if (time.Tick % TICKS_PER_DAY == 0)
        { // new day
            events.Publish(new OnDayStart());

            if (GetDaysInMonth(_calenderComponent.MonthOfYear, _calenderComponent.Year) - (_calenderComponent.DayOfMonth + 1) <= 0)
            { // new month
                events.Publish(new OnMonthStart());
                
                if (_calenderComponent.MonthOfYear == CalenderComponent.Months.DECEMBER)
                { // new year
                    events.Publish(new OnYearStart());
                    
                    _calenderComponent.MonthOfYear = CalenderComponent.Months.JANUARY;
                    _calenderComponent.Year++;
                }
                else
                {
                    _calenderComponent.MonthOfYear++;
                }

                _calenderComponent.DayOfMonth = 1;
            }
            else
            {
                _calenderComponent.DayOfMonth++;
            }

            if (_calenderComponent.DayOfWeek == CalenderComponent.Days.Sunday)
            {
                events.Publish(new OnWeekStart());

                _calenderComponent.DayOfWeek = CalenderComponent.Days.MONDAY;
            }
            else
            {
                _calenderComponent.DayOfWeek++;
            }
            
            Console.WriteLine($"Day started! Day: {_calenderComponent.DayOfWeek} " +
                              $"Month: {_calenderComponent.MonthOfYear} " +
                              $"Date: {_calenderComponent.DayOfMonth}/{_calenderComponent.MonthOfYear}/{_calenderComponent.Year}");
        }

        if (tickOfDay == GetDayStartTick(TICKS_PER_DAY))
        {
            events.Publish(new OnMonthStart());
        } else if (tickOfDay == GetNightStartTick(TICKS_PER_DAY))
        {
            events.Publish(new OnNightStart());
        }
    }
    
    private static uint GetDaysInMonth(CalenderComponent.Months month, int year)
    {
        return month switch
        {
            CalenderComponent.Months.JANUARY => 31U,
            CalenderComponent.Months.FEBRUARY => IsLeapYear(year) ? 29U : 28U,
            CalenderComponent.Months.MARCH => 31U,
            CalenderComponent.Months.APRIL => 30U,
            CalenderComponent.Months.MAY => 31U,
            CalenderComponent.Months.JUNE => 30U,
            CalenderComponent.Months.JULY => 31U,
            CalenderComponent.Months.AUGUST => 31U,
            CalenderComponent.Months.SEPTEMBER => 30U,
            CalenderComponent.Months.OCTOBER => 31U,
            CalenderComponent.Months.NOVEMBER => 30U,
            CalenderComponent.Months.DECEMBER => 31U,
            _ => throw new ArgumentOutOfRangeException(nameof(month))
        };
    }
    
    private static bool IsLeapYear(int year)
    {
        return year % 400 == 0 || (year % 4 == 0 && year % 100 != 0);
    }
    
    private static ulong GetDayStartTick(ulong ticksPerDay)
    {
        return ticksPerDay / 4;
    }

    private static ulong GetNightStartTick(ulong ticksPerDay)
    {
        return ticksPerDay * 3 / 4;
    }
}