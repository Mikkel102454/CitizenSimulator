using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Vault.Components;

public sealed class WalletComponent : Component<WalletComponent>
{
    public decimal Money { get; internal set; }
}