using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.CitizenMind.Definitions;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Models.Abstractions;
using CitizenSimulator.CitizenMind.Planning.Abstractions;

namespace CitizenSimulator.CitizenMind.Services.Abstractions;

public interface ICitizenMindService
{
    public void RegisterFact<T>(string id, Func<EntityRef, T> provider, bool canPredict = false);
    public void RegisterHandler(string id, IMindActionHandler handler);
    public void RegisterGoal(GoalDefinition goal);
    public void RegisterAction(ActionDefinition action);

    public void RegisterTimePredictor(string id, IElapsedTimePredictor predictor);

    public void RegisterCandidateProvider(CandidateProviderDescriptor descriptor, Func<EntityRef, IActionCandidateProvider> factory);
}
