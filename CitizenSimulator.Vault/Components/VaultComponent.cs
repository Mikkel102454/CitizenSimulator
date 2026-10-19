using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Vault.Components;

public sealed class VaultComponent : Component<VaultComponent>
{
    // maybe make this its own compoennt
    public record struct Account
    { public decimal Balance { get; internal set; }
    }

    public List<Account> Accounts { get; internal set; } = [];
    public string Name { get; internal set; } = "";
}