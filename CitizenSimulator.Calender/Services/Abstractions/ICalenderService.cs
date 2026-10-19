namespace CitizenSimulator.Calender.Services.Abstractions;

public interface ICalenderService
{
    ulong GetTicksPerDay();
    string GetTimeOfDay();
}