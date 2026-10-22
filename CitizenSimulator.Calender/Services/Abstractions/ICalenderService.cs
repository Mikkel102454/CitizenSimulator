namespace CitizenSimulator.Calender.Services.Abstractions;

public interface ICalenderService
{
    ulong GetTicksPerDay();
    string GetTimeOfDay();
    
    TimeSpan GetGameTime(ulong tick);
}