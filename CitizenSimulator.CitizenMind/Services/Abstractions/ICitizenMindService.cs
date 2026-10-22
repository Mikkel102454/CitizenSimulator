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
    public void AddGoalActions(string goalId, IEnumerable<string> actionIds);

    public void RegisterTimePredictor(string id, IElapsedTimePredictor predictor);

    /// <summary>
    /// Replaces a citizen's manual one-off commitments at runtime, preserving daily
    /// rules and their occurrences. Existing manual outcomes are retained for unchanged
    /// IDs, goals, windows and targets. Other mod sources are preserved. Requires an attached MindComponent. The ID prefix
    /// "citizen_mind.daily:" is reserved for generated occurrences.
    /// </summary>
    public void SetSchedule(EntityRef citizen, IReadOnlyList<ScheduledCommitment> commitments);

    /// <summary>Replaces only the named mod's commitments, preserving other sources and unchanged outcomes.</summary>
    public void SetScheduleSource(EntityRef citizen, string sourceId, IReadOnlyList<ScheduledCommitment> commitments);

    /// <summary>Returns all schedule sources and generated commitments, including retained outcomes.</summary>
    public IReadOnlyList<ScheduledCommitmentState> GetSchedule(EntityRef citizen);

    /// <summary>Removes every schedule source, daily rules, and occurrence history.</summary>
    public void ClearSchedule(EntityRef citizen);

    /// <summary>
    /// Replaces daily rules. Pending generated occurrences are rebuilt on the next
    /// mind update; completed/missed occurrences and manual commitments are preserved.
    /// An unchanged rule set is a no-op. Reusing a rule ID retains its occurrence history.
    /// </summary>
    public void SetDailySchedule(EntityRef citizen, IReadOnlyList<DailyScheduleRule> rules);
    public IReadOnlyList<DailyScheduleRule> GetDailySchedule(EntityRef citizen);

    /// <summary>Removes daily rules and pending occurrences, preserving manual entries and outcomes.</summary>
    public void ClearDailySchedule(EntityRef citizen);

    public void RegisterCandidateProvider(CandidateProviderDescriptor descriptor, Func<EntityRef, IActionCandidateProvider> factory);
}
