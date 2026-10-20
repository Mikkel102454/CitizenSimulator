namespace CitizenSimulator.CitizenMind.Models.Abstractions;

public interface IMindActionHandler
{
    ActionResult Start(ActionContext context);
    ActionResult Tick(ActionContext context);
    void Cancel(ActionContext context);
}