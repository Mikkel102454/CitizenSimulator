using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.CitizenMind.Definitions;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Models.Abstractions;
using CitizenSimulator.CitizenMind.Planning.Abstractions;
using CitizenSimulator.CitizenMind.Scheduling;

namespace CitizenSimulator.CitizenMind.Services.Abstractions;

public interface ICitizenMindService
{
    public void RegisterFact<T>(string id, Func<EntityRef, T> provider, bool canPredict = false);
    public void RegisterHandler(string id, IMindActionHandler handler);
    public void RegisterGoal(GoalDefinition goal);
    public void RegisterAction(ActionDefinition action);

    public void RegisterTimePredictor(string id, IElapsedTimePredictor predictor);

    /// <summary>
    /// Replaces a citizen's one-off commitments at runtime. Existing outcomes are
    /// retained for unchanged IDs, goals and windows. Requires an attached MindComponent.
    /// </summary>
    public void SetSchedule(EntityRef citizen, IReadOnlyList<ScheduledCommitment> commitments);
    public IReadOnlyList<ScheduledCommitmentState> GetSchedule(EntityRef citizen);
    public void ClearSchedule(EntityRef citizen);

    public void RegisterCandidateProvider(CandidateProviderDescriptor descriptor, Func<EntityRef, IActionCandidateProvider> factory);
}
