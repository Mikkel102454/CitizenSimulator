namespace CitizenSimulator.CitizenMind.Planning;

/// <summary>
/// A deterministic work allowance shared by a search and its nested schedule searches.
/// Node processing, job setup and plan validation are charged conservatively, so
/// the number of node expansions cannot exceed the allowance.
/// </summary>
public sealed class PlanningBudget
{
    public int Remaining { get; private set; }
    public int Consumed { get; private set; }

    public PlanningBudget(int allowance)
    {
        if (allowance < 0)
            throw new ArgumentOutOfRangeException(nameof(allowance));
        Remaining = allowance;
    }

    public bool TryConsume(int amount = 1)
    {
        if (amount < 1)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (amount > Remaining)
            return false;
        Remaining -= amount;
        Consumed += amount;
        return true;
    }
}
