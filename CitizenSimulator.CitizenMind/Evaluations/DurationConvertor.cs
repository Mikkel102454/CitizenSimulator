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
}
