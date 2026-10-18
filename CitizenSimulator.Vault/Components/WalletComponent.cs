using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Vault.Components;

public sealed class WalletComponent : Component<WalletComponent>
{
    public double Money { get; internal set; } = 0D;
}