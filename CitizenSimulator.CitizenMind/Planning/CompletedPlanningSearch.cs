using CitizenSimulator.CitizenMind.Planning.Abstractions;

namespace CitizenSimulator.CitizenMind.Planning;

internal sealed class CompletedPlanningSearch(PlanResult result) : IPlanningSearch
{
    public PlanResult Result { get; } = result;
    public void Advance(PlanningBudget budget) { }
}
