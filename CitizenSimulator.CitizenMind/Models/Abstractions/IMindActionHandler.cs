namespace CitizenSimulator.CitizenMind.Models.Abstractions;

public interface IMindActionHandler
{
    ActionResult Start(ActionContext context);
    ActionResult Tick(ActionContext context);
    
    /// <summary>
    /// Releases remaining reservations and temporary resources.
    /// Must be safe after reported failure or unverified success.
    /// Must not undo committed world changes.
    /// </summary>
    void Cancel(ActionContext context);
}