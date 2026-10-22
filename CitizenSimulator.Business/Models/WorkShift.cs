namespace CitizenSimulator.Business.Models;

public sealed record WorkShift(Employment Employment, TimeSpan Start, TimeSpan End);
