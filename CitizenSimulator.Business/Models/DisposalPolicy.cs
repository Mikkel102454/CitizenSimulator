namespace CitizenSimulator.Business.Models;

public class DisposalPolicy
{
    public bool PermanentStorage { get; init; } = true;
    public List<string> AcceptedItems { get; init; } = [];
    public decimal CapacityTonnes { get; init; }
    public decimal MaxIntakeTonnesPerDay { get; init; }
    public decimal FeePerTonne { get; init; }
}
