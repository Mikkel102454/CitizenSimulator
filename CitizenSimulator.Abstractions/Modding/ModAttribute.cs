namespace CitizenSimulator.Abstractions.Modding;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ModAttribute : Attribute
{
    public string Id { get; }
    public string Name { get; }
    public string Version { get; }

    public string[] Dependencies { get; set; } = [];

    public ModAttribute(string id, string name, string version)
    {
        Id = id;
        Name = name;
        Version = version;
    }
}