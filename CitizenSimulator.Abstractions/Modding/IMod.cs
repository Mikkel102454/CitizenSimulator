namespace CitizenSimulator.Abstractions.Modding;

public interface IMod
{
    void OnLoad(IModContext context);
}