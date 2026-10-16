using System.Collections.Concurrent;
using CitizenSimulator.Abstractions.Input;
using CitizenSimulator.Engine.Events;
namespace CitizenSimulator.Engine.Input;
// input disptacher for key inputs as we dont want to use the normal event handler cause it only runs on tick
// so it wont run when sim is paused
public sealed class InputDispatcher(InputState state, EventBus events)
{
    private readonly ConcurrentQueue<object> _pending = new();
    private KeyModifiers _modifiers;
    public void Press(InputKey key, KeyModifiers modifiers = KeyModifiers.None) => _pending.Enqueue(new KeyPressed(key, modifiers));
    public void Release(InputKey key, KeyModifiers modifiers = KeyModifiers.None) => _pending.Enqueue(new KeyReleased(key, modifiers));
    public void SubmitLine(string text) => _pending.Enqueue(new LineSubmitted(text));
    public void Reset() => _pending.Enqueue(new ResetInput());
    private sealed record ResetInput;
    public void Drain()
    {
        for (var i = 0; i < 64 && _pending.TryDequeue(out var message); i++)
        {
            switch (message)
            {
                case KeyPressed pressed:
                    _modifiers = pressed.Modifiers;
                    if (pressed.Key != InputKey.Unknown && state.Press(pressed.Key)) events.Publish(pressed);
                    break;
                case KeyReleased released:
                    _modifiers = released.Modifiers;
                    if (state.Release(released.Key)) events.Publish(released);
                    break;
                case LineSubmitted line: events.Publish(line); break;
                case ResetInput:
                    foreach (var key in state.GetHeldKeys())
                    {
                        state.Release(key);
                        events.Publish(new KeyReleased(key, KeyModifiers.None));
                    }
                    _modifiers = KeyModifiers.None;
                    break;
            }
        }
    }
    public void PublishHeldEvents()
    {
        foreach (var key in state.GetHeldKeys()) events.Publish(new KeyHeld(key, _modifiers));
    }
}
