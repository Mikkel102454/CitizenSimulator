using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Vault.Components;

public sealed class VaultComponent : Component<VaultComponent>
{ 
    public List<BankAccountComponent> Accounts { get; internal set; } = [];
    public string Name { get; internal set; } = "";
}