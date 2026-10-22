using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Calender.Services.Abstractions;
using CitizenSimulator.CitizenMind.Configuration;
using CitizenSimulator.CitizenMind.Executor;
using CitizenSimulator.CitizenMind.Executor.Handlers;
using CitizenSimulator.CitizenMind.Services;
using CitizenSimulator.CitizenMind.Services.Abstractions;
using CitizenSimulator.CitizenMind.Systems;

namespace CitizenSimulator.CitizenMind;

[Mod("citizen_mind", "CitizenMind", "1.0.0", Dependencies = new[] { "calender" })]
public class CitizenMind : IMod
{
    public void OnLoad(IModContext context)
    {
        var service = new CitizenMindService(context.Store);
        context.Services.Register<ICitizenMindService>(service);
        service.RegisterHandler("citizen_mind.wait_until", new WaitUntilHandler());
        context.Systems.Register(new CitizenMindSystem(context.Events, new GoalSelector(service), service,
            context.Services.Get<ICalenderService>(), new ActionExecutor(service)));
        MindDefinitionLoader.Load(service, typeof(CitizenMind).Assembly,
            new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>
            {
                ["waitTargets"] = [new Dictionary<string, string> { ["untilMinutes"] = "0" }]
            });
    }
}
