using System.Text.Json;

namespace CitizenSimulator.Items.Models;

public class Effect
{
    public string Type { get; init; } = "";

    public Dictionary<string, JsonElement> Values { get; init; } = [];
}