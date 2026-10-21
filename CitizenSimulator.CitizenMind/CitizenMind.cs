using System.Reflection;
using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Planning;
using CitizenSimulator.CitizenMind.Services;
using CitizenSimulator.CitizenMind.Services.Abstractions;
using CitizenSimulator.CitizenMind.Systems;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CitizenSimulator.CitizenMind;

[Mod("citizen_mind", "CitizenMind", "1.0.0")]
public class CitizenMind : IMod
{
    public void OnLoad(IModContext context)
    {
        CitizenMindService citizenMindService = new CitizenMindService();
        context.Services.Register<ICitizenMindService>(citizenMindService);
        
        GoalSelector goalSelector = new GoalSelector(citizenMindService);
        context.Systems.Register(new CitizenMindSystem(context.Events, goalSelector));
        
        var assembly = typeof(CitizenMind).Assembly;

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        
        foreach (string name in assembly.GetManifestResourceNames())
        {
            if (!name.Contains(".Data.Goals.", StringComparison.Ordinal) ||
                !name.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"Cannot read embedded goal '{name}'.");

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

            using var stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"Cannot read embedded action '{name}'.");

            using var reader = new StreamReader(stream);

            ActionDefinition action = deserializer.Deserialize<ActionDefinition>(reader.ReadToEnd());

            citizenMindService.RegisterAction(action);
        }
        
        var state = new EvaluationContext
        {
            Facts = new Dictionary<string, object?>
            {
                ["needs.thirst"] = 0.73,
                ["home.can_drink"] = true,
                ["needs.hunger"] = 0.8,
                ["inventory.food_count"] = 0,
                ["finance.money"] = 10,
                ["shops.cheap_available"] = true,
                ["shops.fast_available"] = true
            }
        };

        foreach (var goal in citizenMindService.Goals.Values)
        {
            PlanResult result = ActionPlanner.FindPlan(goal, state, citizenMindService.Actions, new PlannerOptions());
            
            string breakr = "";
        }
    }
}