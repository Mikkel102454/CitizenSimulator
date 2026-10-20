using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.CitizenMind.Components;
using CitizenSimulator.CitizenMind.Services;

namespace CitizenSimulator.CitizenMind.Systems;

public class CitizenMindSystem(IEventBus events, GoalSelector goalSelector) : GameSystem
{
    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {
        foreach (var entity in Store.Query(MindComponent.GetComponentType()))
        {
            MindComponent mind = Store.GetComponent<MindComponent>(entity);
            mind.CurrentGoal = goalSelector.SelectGoals(mind);
        }
    }
}