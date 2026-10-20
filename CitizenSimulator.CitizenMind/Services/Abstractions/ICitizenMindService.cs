using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Models.Abstractions;

namespace CitizenSimulator.CitizenMind.Services.Abstractions;

public interface ICitizenMindService
{
    void RegisterFact<T>(string id, Func<EntityRef, T> provider);
    void RegisterHandler(string id, IMindActionHandler handler);
    void RegisterGoal(GoalDefinition goal);
}