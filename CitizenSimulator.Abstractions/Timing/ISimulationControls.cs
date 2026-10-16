namespace CitizenSimulator.Abstractions.Timing;
// Requests are applied between ticks in submission order. Clock state changes after application.
public interface ISimulationControls
{
    void Pause();
    void Resume();
    void SetSpeed(double speed);
    void Step(ulong count = 1);
    void AdvanceBy(TimeSpan duration);
    void AdvanceTo(TimeSpan target);
    void Quit();
}
