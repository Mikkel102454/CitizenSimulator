using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Vault.Components;

public class BankAccountComponent : Component<BankAccountComponent>
{
    public decimal Balance { get; internal set; }
    public required VaultComponent Vault { get; init; }
}