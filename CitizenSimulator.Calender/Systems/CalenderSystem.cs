using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Calender.Components;
using CitizenSimulator.Calender.Events;
using CitizenSimulator.Calender.Services;

namespace CitizenSimulator.Calender.Systems;

public class CalenderSystem(IEventBus events, CalenderService calenderService) : GameSystem
{
    private CalenderComponent _calenderComponent = null!;
    private DateOnly _currentDate;

    protected override void OnStart(ICommandBuffer commands)
    {
        EntityRef entity = commands.CreateEntity();
        _calenderComponent = commands.AddComponent(entity, new CalenderComponent());

        SetDate(calenderService.GetDate(TimeSpan.Zero));

        CalenderComponent.Date currentDate = new CalenderComponent.Date(_calenderComponent.DayOfMonth, _calenderComponent.MonthOfYear,  _calenderComponent.Year);

        events.Publish(new OnDayStart(currentDate));
        events.Publish(new OnMonthStart(currentDate));
        events.Publish(new OnYearStart(currentDate));
        if (_currentDate.DayOfWeek == DayOfWeek.Monday)
            events.Publish(new OnWeekStart(currentDate));
    }

    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {
        ulong tickOfDay = time.Tick % calenderService.GetTicksPerDay();
        calenderService.TickOfDay = tickOfDay;
        var date = calenderService.GetDate(calenderService.GetGameTime(time.Tick));

        if (date != _currentDate)
        { // new day
            var previousDate = _currentDate;
            SetDate(date);
            CalenderComponent.Date currentDate = new CalenderComponent.Date(_calenderComponent.DayOfMonth, _calenderComponent.MonthOfYear,  _calenderComponent.Year);

            events.Publish(new OnDayStart(currentDate));

            if (date.Month != previousDate.Month || date.Year != previousDate.Year)
                events.Publish(new OnMonthStart(currentDate));
            if (date.Year != previousDate.Year)
                events.Publish(new OnYearStart(currentDate));
            if (date.DayOfWeek == DayOfWeek.Monday)
                events.Publish(new OnWeekStart(currentDate));
        }

        if (tickOfDay == GetDayStartTick(calenderService.GetTicksPerDay()))
        {
            CalenderComponent.Date currentDate = new CalenderComponent.Date(_calenderComponent.DayOfMonth, _calenderComponent.MonthOfYear,  _calenderComponent.Year);
            events.Publish(new OnMorningStart(currentDate));
        } else if (tickOfDay == GetNightStartTick(calenderService.GetTicksPerDay()))
        {
            CalenderComponent.Date currentDate = new CalenderComponent.Date(_calenderComponent.DayOfMonth, _calenderComponent.MonthOfYear,  _calenderComponent.Year);
            events.Publish(new OnNightStart(currentDate));
        }

    }

    private void SetDate(DateOnly date)
    {
        _currentDate = date;
        _calenderComponent.DayOfMonth = (uint)date.Day;
        _calenderComponent.MonthOfYear = (CalenderComponent.Months)(date.Month - 1);
        _calenderComponent.Year = date.Year;
        _calenderComponent.DayOfWeek = (CalenderComponent.Days)(((int)date.DayOfWeek + 6) % 7);
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
