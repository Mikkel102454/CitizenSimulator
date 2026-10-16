namespace CitizenSimulator.Abstractions.Input;

public interface IInputState
{
    bool IsDown(InputKey key);
}