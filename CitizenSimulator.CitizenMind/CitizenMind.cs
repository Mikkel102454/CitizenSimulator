using System.Reflection;
using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Calender.Services.Abstractions;
using CitizenSimulator.CitizenMind.Executor;
using CitizenSimulator.CitizenMind.Executor.Handlers;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Services;
using CitizenSimulator.CitizenMind.Services.Abstractions;
using CitizenSimulator.CitizenMind.Systems;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CitizenSimulator.CitizenMind;

[Mod("citizen_mind", "CitizenMind", "1.0.0", Dependencies = new[] { "calender" })]
public class CitizenMind : IMod
{
    public void OnLoad(IModContext context)
    {
        var citizenMindService = new CitizenMindService(context.Store);
        context.Services.Register<ICitizenMindService>(citizenMindService);
        citizenMindService.RegisterHandler("citizen_mind.wait_until", new WaitUntilHandler());

        var goalSelector = new GoalSelector(citizenMindService);
        ICalenderService calenderService = context.Services.Get<ICalenderService>();

        var executor = new ActionExecutor(citizenMindService);

        context.Systems.Register(new CitizenMindSystem(context.Events, goalSelector, citizenMindService, calenderService, executor));

        Assembly assembly = typeof(CitizenMind).Assembly;

        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        foreach (string name in assembly.GetManifestResourceNames())
        {
            if (!name.Contains(".Data.Goals.", StringComparison.Ordinal) ||
                !name.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using Stream stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"Cannot read embedded goal '{name}'.");

            using var reader = new StreamReader(stream);

            GoalDefinition goal = deserializer.Deserialize<GoalDefinition>(reader.ReadToEnd());

            citizenMindService.RegisterGoal(goal);
        }

        foreach (string name in assembly.GetManifestResourceNames())
        {
            if (!name.Contains(".Data.Actions.", StringComparison.Ordinal) ||
                !name.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using Stream stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"Cannot read embedded action '{name}'.");

            using var reader = new StreamReader(stream);

            ActionDefinition action = deserializer.Deserialize<ActionDefinition>(reader.ReadToEnd());

            citizenMindService.RegisterAction(action);
        }
    }
}
