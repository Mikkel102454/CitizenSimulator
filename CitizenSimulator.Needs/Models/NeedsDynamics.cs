namespace CitizenSimulator.Needs.Models;

public static class NeedsDynamics
{
    public const double ThirstChangePerDay = 0.34;
    public const double HungerChangePerDay = 0.10;
    public const double EnergyChangePerDay = -0.30;

    public static double Advance(double current, double changePerDay, TimeSpan elapsed)
    {
        return Math.Clamp(current + changePerDay * elapsed.TotalDays, 0.0, 1.0);
    }
}
