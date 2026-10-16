
using System.Reflection;
using System.Runtime.Loader;
using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Engine.ECS;

namespace CitizenSimulator.Engine.Modding;

public sealed class ModLoader(ComponentRegistry componentRegistry)
{
    private readonly Dictionary<string, LoadedMod> _mods = new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _initialized = new(StringComparer.OrdinalIgnoreCase);

    private bool _loaded;
    
    public void LoadMods(string folder, IModContext context)
    {
        if(_loaded)
            throw new InvalidOperationException("Mods have already been loaded");
        _loaded = true;
        
        Directory.CreateDirectory(folder);
        folder = Path.GetFullPath(folder);

        DiscoverMods(folder);
        var sortedMods = ResolveDependencies(); //we sort for mod dependency
        
        // register components from valid mod assemblies
        foreach (var assembly in sortedMods.Select(mod => mod.Instance.GetType().Assembly).Distinct())
        {
            componentRegistry.RegisterFromAssembly(assembly);
        }

        
        InitializeMods(sortedMods, context);
    }

    private void DiscoverMods(string folder)
    {
        var dlls = Directory.GetFiles(folder, "*.dll", SearchOption.AllDirectories);

        // asm name to path
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dll in dlls)
        {
            string name = Path.GetFileNameWithoutExtension(dll);
            paths[name] = Path.GetFullPath(dll);
        }

        // resolve references between mods
        AssemblyLoadContext.Default.Resolving += (ctx, name) =>
        {
            if (name.Name == null || !paths.TryGetValue(name.Name, out var path))
                return null;

            return ctx.LoadFromAssemblyPath(path);
        };

        var discovered = new List<LoadedMod>();

        //loop and discover mods inside dll files
        foreach (var dll in dlls.OrderBy(x => x))
        {
            try
            {
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(dll));

                foreach (var type in assembly.GetTypes())
                {
                    //see if they implement mod interface
                    if (type.IsAbstract || type.IsInterface || !typeof(IMod).IsAssignableFrom(type))
                        continue;

                    //read mod attribute
                    var metadata = type.GetCustomAttribute<ModAttribute>();

                    if (metadata is null)
                        continue;

                    // create an instance so from the type instead of class like you normally do (we dont know the class bleeeh)
                    if (Activator.CreateInstance(type) is not IMod mod)
                        continue;

                    if (string.IsNullOrWhiteSpace(metadata.Id))
                    {
                        Console.WriteLine($"[ERROR] Mod ID is empty: {type.Name}");
                        continue;
                    }

                    discovered.Add(new LoadedMod(mod, metadata));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[ERROR] Could not discover {dll}: {ex.Message}");
            }
        }

        // make sure all id's are unique.
        foreach (var group in discovered.GroupBy(m => m.Metadata.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() > 1)
            {
                Console.WriteLine($"[ERROR] Duplicate mod ID: {group.Key}");
                continue;
            }

            var mod = group.Single();
            _mods.Add(mod.Metadata.Id, mod);

            Console.WriteLine($"[DISCOVERED] {mod.Metadata.Name}");
        }
    }

    private List<LoadedMod> ResolveDependencies()
    {
        var result = new List<LoadedMod>();

        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var valid = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var invalid = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool Visit(string id)
        {
            if (valid.Contains(id))
                return true;

            if (invalid.Contains(id))
                return false;

            if (!_mods.TryGetValue(id, out var mod))
            {
                Console.WriteLine($"[ERROR] Missing dependency: {id}");
                return false;
            }

            if (!visiting.Add(id))
            {
                Console.WriteLine($"[ERROR] Circular dependency involving: {id}");
                return false;
            }

            bool dependenciesValid = true;

            foreach (var dependency in mod.Metadata.Dependencies ?? [])
            {
                if (!Visit(dependency))
                    dependenciesValid = false;
            }

            visiting.Remove(id);

            if (!dependenciesValid)
            {
                invalid.Add(id);
                return false;
            }

            valid.Add(id);
            result.Add(mod);
            return true;
        }

        foreach (var id in _mods.Keys.OrderBy(x => x))
            Visit(id);

        return result;
    }

    private void InitializeMods(List<LoadedMod> sortedMods, IModContext context)
    {
        foreach (var mod in sortedMods)
        {
            var metadata = mod.Metadata;

            // init if all depends are valid
            bool ready = (metadata.Dependencies ?? []).All(dep => _initialized.Contains(dep));

            if (!ready)
            {
                Console.WriteLine($"[SKIPPED] {metadata.Name}: dependency failed");
                continue;
            }

            try
            {
                mod.Instance.OnLoad(context);

                _initialized.Add(metadata.Id);

                Console.WriteLine($"[LOADED] {metadata.Name} v{metadata.Version}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {metadata.Name}: {ex.Message}");
            }
        }
    }
}
