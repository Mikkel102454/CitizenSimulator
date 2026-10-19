using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Property.Components;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Business.Components;

public sealed class BusinessComponent : Component<BusinessComponent>
{
    public Models.Business Type { get; internal set; }

    public string Name { get; internal set; } = "";

    public CitizenComponent Owner { get; internal set; }

    public PropertyComponent Property { get; internal set; }
    public InventoryComponent Inventory { get; internal set; }
    public VaultComponent.Account PaymentAccount { get; internal set; }

    public bool IsOperating { get; internal set; } = false;
}
