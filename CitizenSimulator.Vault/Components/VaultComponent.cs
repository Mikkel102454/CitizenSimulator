using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Citizen.Components;

namespace CitizenSimulator.Vault.Components;

public class VaultComponent : Component<VaultComponent>
{
    public record struct Account
    { 
        public CitizenComponent Citizen { get; internal set; }
        public double Balance { get; internal set; }
    }

    public List<Account> Accounts { get; internal set; } = [];
    public string Name { get; internal set; } = "";
}