namespace CitizenSimulator.Business.Models;

public class PremisesRequirements
{
    public List<string> AllowedUses { get; init; } = [];
    public decimal MinimumAreaM2 { get; init; }
    public decimal StorageCapacity { get; init; }
}
