using CitizenSimulator.Abstractions.Events;
namespace CitizenSimulator.Abstractions.Input;
public sealed record ControlFailed(string Message) : IEvent;
