using CitizenSimulator.Abstractions.Events;

namespace CitizenSimulator.Abstractions.Input;

public enum InputKey
{
    Unknown,
    Enter,
    Escape,
    Space,
    Left,
    Right,
    Up,
    Down,
    Backspace, Tab, Shift, Control, Alt,
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12
}

[Flags]
public enum KeyModifiers
{
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 4
}

public sealed record KeyPressed(InputKey Key, KeyModifiers Modifiers) : IEvent;

public sealed record KeyReleased(InputKey Key, KeyModifiers Modifiers) : IEvent;

public sealed record KeyHeld(InputKey Key, KeyModifiers Modifiers) : IEvent;

public sealed record LineSubmitted(string Text) : IEvent;
