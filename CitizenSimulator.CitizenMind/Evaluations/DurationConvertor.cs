namespace CitizenSimulator.CitizenMind.Evaluations;

internal static class DurationConverter
{
    public static double GetMinutesPerUnit(string unit)
    {
        return unit switch
        {
            "simulationMinutes" => 1,
            "simulationHours" => 60,
            "simulationDays" => 1440,

            _ => throw new InvalidOperationException($"Unsupported duration unit '{unit}'.")
        };
    }

    public static double ToMinutes(double duration, string unit)
    {
        double minutes = duration * GetMinutesPerUnit(unit);

        if (!double.IsFinite(minutes) || minutes < 0)
            throw new InvalidOperationException("Duration must be finite and nonnegative.");

        return minutes;
    }
    
    public static TimeSpan ToPositiveTimeSpan(double minutes)
    {
        if (!double.IsFinite(minutes) || minutes <= 0)
            throw new InvalidOperationException("Timeout must be finite and greater than zero.");

        TimeSpan duration;

        try
        {
            duration = TimeSpan.FromMinutes(minutes);
        }
        catch (OverflowException exception)
        {
            throw new InvalidOperationException("Timeout exceeds the supported time range.", exception);
        }

        if (duration <= TimeSpan.Zero)
            throw new InvalidOperationException("Timeout is smaller than the supported time resolution.");

        return duration;
    }
}
