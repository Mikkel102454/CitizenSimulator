using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Calender.Components;

public class CalenderComponent : Component<CalenderComponent>
{
    public enum Days
    {
        MONDAY = 0,
        TUESDAY = 1,
        WEDNESDAY = 2,
        Thursday = 3,
        Friday = 4,
        Saturday = 5,
        Sunday = 6
    }
    public enum Months
    {
        JANUARY = 0,
        FEBRUARY = 1,
        MARCH = 2,
        APRIL = 3,
        MAY = 4,
        JUNE = 5,
        JULY = 6,
        AUGUST = 7,
        SEPTEMBER = 8,
        OCTOBER = 9,
        NOVEMBER = 10,
        DECEMBER = 11,
    }
    public Days DayOfWeek { get; internal set; } = 0;
    public uint DayOfMonth { get; internal set; } = 0;
    public Months MonthOfYear { get; internal set; } = 0;
    public int Year { get; internal set; } = 0;
}