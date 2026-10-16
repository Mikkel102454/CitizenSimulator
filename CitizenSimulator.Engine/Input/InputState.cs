using CitizenSimulator.Abstractions.Input;

namespace CitizenSimulator.Engine.Input;

public sealed class InputState : IInputState
{
    private readonly HashSet<InputKey> _down = [];

    public bool IsDown(InputKey key) => _down.Contains(key);

    internal bool Press(InputKey key) => _down.Add(key);

    internal bool Release(InputKey key) => _down.Remove(key);

    internal InputKey[] GetHeldKeys() => _down.ToArray();
}