using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Citizen.Services.Abstraction;
using CitizenSimulator.Housing.Services.Abstractions;
using CitizenSimulator.Property.Components;
using CitizenSimulator.Property.Services.Abstractions;

namespace CitizenSimulator.Initializer;

// Keep current when adding mods: required dependencies load first.
[Mod("initializer", "Initializer", "1.0.0", Dependencies = new[]
{
    "business", "citizen", "housing",
    "inventory", "items", "needs", "property", "vault"
})]
public sealed class Initializer : IMod
{
    public void OnLoad(IModContext context)
    {
        var citizens = context.Services.Get<ICitizenService>();
        var properties = context.Services.Get<IPropertyService>();
        var housing = context.Services.Get<IHousingService>();
        var businesses = context.Services.Get<IBusinessService>();
        var commands = new ImmediateCommands(context.Store);
        var random = new Random(42);
        var people = new List<CitizenComponent>();
        var homes = new List<SeedHome>();
        var premises = new List<SeedBusiness>();
        (string Id, int Count)[] townBusinesses =
        [
            ("business.supermarket", 4), ("business.bakery", 4),
            ("business.wheat_farm", 2), ("business.flour_mill", 2),
            ("business.furniture_workshop", 1), ("business.water_works", 1),
            ("business.waste_to_energy_facility", 1), ("business.landfill", 1)
        ];
        var catalog = businesses.GetBusinessTypes().ToDictionary(type => type.Id);
        foreach (var (id, _) in townBusinesses)
            Require(catalog.ContainsKey(id), $"Missing business definition '{id}'.");
        var skills = townBusinesses.SelectMany(entry => catalog[entry.Id].Workforce)
            .GroupBy(role => role.Role).OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Max(role => role.MinimumSkill));

        for (int i = 0; i < 200; i++)
        {
            var entity = commands.CreateEntity();
            Require(citizens.CreateCitizen(commands, entity, $"Citizen {i + 1:000}", random.Next(20, 61), out var person)
                && person != null, $"Could not create citizen on entity {entity.Id}.");
            people.Add(person!);
            commands.AddComponent(entity, new WorkerProfileComponent
            {
                Skills = skills.ToDictionary(entry => entry.Key, entry => entry.Value + random.Next(3)),
                CommuteMinutes = 15
            });
        }

        for (int i = 0; i < 240; i++)
        {
            var entity = commands.CreateEntity();
            Require(properties.CreateProperty(commands, entity, PropertyComponent.PropertyType.Residential,
                1, random.Next(60, 121), 12000, people[i % 20], out var property) && property != null,
                $"Could not create home property on entity {entity.Id}.");
            Require(housing.CreateHousing(commands, property!, 1, 100, true, true, true, out var house)
                && house != null, $"Could not attach housing to entity {entity.Id}.");
            homes.Add(new SeedHome(house!, i < 160, i < 160 ? random.Next(8000, 14001) : random.Next(40, 81)));
        }

        foreach (var (id, count) in townBusinesses)
        {
            var type = catalog[id];
            Require(type.Premises.AllowedUses.Count > 0 && Enum.TryParse<PropertyComponent.PropertyType>(
                type.Premises.AllowedUses[0], true, out _), $"Invalid premises use for '{id}'.");
            var use = Enum.Parse<PropertyComponent.PropertyType>(type.Premises.AllowedUses[0], true);
            for (int i = 0; i < count; i++)
            {
                var entity = commands.CreateEntity();
                Require(properties.CreateProperty(commands, entity, use, 20,
                    Math.Max(100, type.Premises.MinimumAreaM2), 25000, people[premises.Count % 20], out var property)
                    && property != null, $"Could not create premises for '{id}' on entity {entity.Id}.");
                premises.Add(new SeedBusiness(id, $"{type.Name} {i + 1}", property!));
            }
        }

        context.Systems.Register(new InitializerSystem(context.Services, people, homes, premises));
        Console.WriteLine($"[INITIALIZER] Seed 42: {people.Count} citizens, {homes.Count} homes " +
            $"(160 for sale, 80 for rent), {premises.Count} business premises. Waiting for citizen services.");
    }

    internal static void Require(bool success, string message)
    {
        if (!success) throw new InvalidOperationException($"Initializer: {message}");
    }

    // Only used during OnLoad, before the scheduler locks structural changes.
    private sealed class ImmediateCommands(IEntityStore store) : ICommandBuffer
    {
        public EntityRef CreateEntity() => store.CreateEntity();
        public void DestroyEntity(EntityRef entity) => store.DestroyEntity(entity);
        public void TryDestroyEntity(EntityRef entity)
        {
            if (store.IsAlive(entity)) store.DestroyEntity(entity);
        }
        public T AddComponent<T>(EntityRef entity, T component) where T : class, IComponent => store.AddComponent(entity, component);
        public void RemoveComponent<T>(EntityRef entity) where T : class, IComponent => store.RemoveComponent<T>(entity);
    }
}
