namespace CitizenSimulator.CitizenMind.Planning.Abstractions;

public interface IPlanningSearch
{
    /// <summary>Null while paused or still searching; tick budgets are not terminal failures.</summary>
    PlanResult? Result { get; }

    /// <summary>Advances using only the supplied allowance, including nested searches.</summary>
    void Advance(PlanningBudget budget);
}
