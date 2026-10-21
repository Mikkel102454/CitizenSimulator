namespace CitizenSimulator.CitizenMind.Planning;

internal static class PredictionOperations
{
    public static bool ReadsTarget(string operation)
    {
        return operation switch
        {
            "set" => false,
            "add" or "subtract" => true,
            
            _ => throw new InvalidOperationException($"Unsupported prediction operation '{operation}'.")
        };
    }
}