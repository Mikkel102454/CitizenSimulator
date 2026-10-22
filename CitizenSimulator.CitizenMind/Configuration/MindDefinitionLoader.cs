using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Services.Abstractions;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CitizenSimulator.CitizenMind.Configuration;

/// <summary>Loads behavior from YAML; expansion values supply catalog data, never behavior expressions.</summary>
public static class MindDefinitionLoader
{
    private static readonly ConcurrentDictionary<(Assembly Assembly, string Resource), string> Resources = new();
    private static readonly Regex Placeholder = new(@"\$\{([A-Za-z][A-Za-z0-9]*)\}", RegexOptions.CultureInvariant);

    public static void Load(ICitizenMindService mind, Assembly assembly,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>? expansions = null)
    {
        var deserializer = CreateDeserializer();
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal).ToArray();

        foreach (string category in new[] { "Goals", "Actions", "GoalActions" })
        {
            foreach (string resource in resources.Where(name => name.Contains($".Data.{category}.", StringComparison.Ordinal)))
            {
                try
                {
                    foreach (string yaml in ExpandDocuments(assembly, resource, expansions))
                    {
                        switch (category)
                        {
                            case "Goals":
                                var goal = deserializer.Deserialize<GoalDefinition>(yaml);
                                goal.YamlSource = resource;
                                mind.RegisterGoal(goal);
                                break;
                            case "Actions":
                                var action = deserializer.Deserialize<ActionDefinition>(yaml);
                                action.YamlSource = resource;
                                mind.RegisterAction(action);
                                break;
                            case "GoalActions":
                                var extension = deserializer.Deserialize<GoalActionExtension>(yaml);
                                mind.AddGoalActions(extension.GoalId, extension.Actions);
                                break;
                        }
                    }
                }
                catch (Exception error)
                {
                    throw new InvalidOperationException($"Cannot load CitizenMind YAML '{resource}'.", error);
                }
            }
        }
    }

    public static ActionDefinition ReadAction(Assembly assembly, string resource,
        IReadOnlyDictionary<string, string> variables)
    {
        var root = ReadDocument(assembly, resource);
        root.Children.Remove(new YamlScalarNode("expand"));
        root.Children.Remove(new YamlScalarNode("requires"));
        Substitute(root, variables);
        var action = CreateDeserializer().Deserialize<ActionDefinition>(WriteDocument(root));
        action.YamlSource = resource;
        action.ResolvedParameters = ParameterParser.ParseAll(action.Parameters, action.Id);
        return action;
    }

    private static IEnumerable<string> ExpandDocuments(Assembly assembly, string resource,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>? expansions)
    {
        var root = ReadDocument(assembly, resource);
        if (root.Children.TryGetValue(new YamlScalarNode("requires"), out var required))
        {
            if (GetExpansion(expansions, required).Count == 0)
                yield break;
            root.Children.Remove(new YamlScalarNode("requires"));
        }
        if (!root.Children.TryGetValue(new YamlScalarNode("expand"), out var group))
        {
            Substitute(root, new Dictionary<string, string>());
            yield return WriteDocument(root);
            yield break;
        }

        foreach (var values in GetExpansion(expansions, group))
        {
            var expanded = ReadDocument(assembly, resource);
            expanded.Children.Remove(new YamlScalarNode("expand"));
            expanded.Children.Remove(new YamlScalarNode("requires"));
            Substitute(expanded, values);
            yield return WriteDocument(expanded);
        }
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> GetExpansion(
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>? expansions, YamlNode name)
    {
        if (name is not YamlScalarNode { Value: { } group } || expansions == null || !expansions.TryGetValue(group, out var values))
            throw new InvalidOperationException($"Unknown YAML expansion group '{name}'.");
        return values;
    }

    private static YamlMappingNode ReadDocument(Assembly assembly, string resource)
    {
        string yaml = Resources.GetOrAdd((assembly, resource), key =>
        {
            using var stream = key.Assembly.GetManifestResourceStream(key.Resource)
                ?? throw new InvalidOperationException($"Missing YAML resource '{key.Resource}'.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });
        var documents = new YamlStream();
        documents.Load(new StringReader(yaml));
        if (documents.Documents.Count != 1 || documents.Documents[0].RootNode is not YamlMappingNode root)
            throw new InvalidOperationException($"'{resource}' must contain one YAML mapping document.");
        return root;
    }

    private static void Substitute(YamlNode node, IReadOnlyDictionary<string, string> values)
    {
        switch (node)
        {
            case YamlScalarNode { Value: { } value } scalar:
                scalar.Value = Placeholder.Replace(value, match => values.TryGetValue(match.Groups[1].Value, out var replacement)
                    ? replacement : throw new InvalidOperationException($"Unknown YAML variable '{match.Value}'."));
                if (scalar.Value != value)
                    scalar.Style = ScalarStyle.DoubleQuoted;
                break;
            case YamlSequenceNode sequence:
                foreach (var child in sequence.Children.ToArray())
                {
                    if (child is YamlMappingNode entry && !IncludeEntry(entry, values))
                    {
                        sequence.Children.Remove(child);
                        continue;
                    }
                    Substitute(child, values);
                }
                break;
            case YamlMappingNode mapping:
                foreach (var child in mapping.Children.Values)
                    Substitute(child, values);
                break;
        }
    }

    private static bool IncludeEntry(YamlMappingNode entry, IReadOnlyDictionary<string, string> values)
    {
        var key = new YamlScalarNode("whenNonzero");
        if (!entry.Children.TryGetValue(key, out var condition))
            return true;
        if (condition is not YamlScalarNode { Value: { } name } || !values.TryGetValue(name, out var text)
            || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) || !double.IsFinite(number))
            throw new InvalidOperationException($"whenNonzero requires a finite numeric expansion variable: '{condition}'.");
        entry.Children.Remove(key);
        return number != 0;
    }

    private static string WriteDocument(YamlMappingNode root)
    {
        using var writer = new StringWriter();
        new YamlStream(new YamlDocument(root)).Save(writer, assignAnchors: false);
        return writer.ToString();
    }

    private static IDeserializer CreateDeserializer() => new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithTypeConverter(new LiteralConverter())
        .Build();

    // Untyped YAML scalars otherwise become strings or small unsigned integers.
    // Expressions require bool/int/double literals; money remains a typed DECIMAL parameter.
    private sealed class LiteralConverter : IYamlTypeConverter
    {
        public bool Accepts(Type type) => type == typeof(object);

        public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        {
            var scalar = parser.Consume<Scalar>();
            string text = scalar.Value;
            if (scalar.Style is ScalarStyle.SingleQuoted or ScalarStyle.DoubleQuoted)
                return text;
            if (bool.TryParse(text, out bool boolean))
                return boolean;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer))
                return integer;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && double.IsFinite(number))
                return number;
            return text;
        }

        public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
            throw new NotSupportedException("This converter only reads behavior YAML.");
    }

    private sealed class GoalActionExtension
    {
        public required string GoalId { get; init; }
        public required List<string> Actions { get; init; }
    }
}
