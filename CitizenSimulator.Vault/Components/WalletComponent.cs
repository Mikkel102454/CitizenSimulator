using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Vault.Components;

public class WalletComponent : Component<WalletComponent>
{
    public double Money { get; internal set; } = 0D;
}