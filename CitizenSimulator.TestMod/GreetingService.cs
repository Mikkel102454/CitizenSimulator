using CitizenSimulator.TestMod.Services;

namespace CitizenSimulator.TestMod;

public sealed class GreetingService : IGreetingService
{
    public string Greet(string name) => $"Hello, {name}!";
}
