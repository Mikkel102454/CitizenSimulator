namespace CitizenSimulator.Business.Models;

public class OpeningHours
{
    public List<string> Days { get; init; } = [];
    public string Opens { get; init; } = "";
    public string Closes { get; init; } = "";
}
