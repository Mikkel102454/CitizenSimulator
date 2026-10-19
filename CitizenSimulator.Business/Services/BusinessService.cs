using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Property.Components;
using CitizenSimulator.Vault.Components;
using CitizenSimulator.Vault.Services.Abstractions;

namespace CitizenSimulator.Business.Services;

public class BusinessService(IEntityStore store, IEventBus events, IVaultService vaultService) : IBusinessService
{
    public bool CreateBusiness(ICommandBuffer commands, string typeId, string name, CitizenComponent owner, PropertyComponent property,
        out BusinessComponent outBusiness)
    {
        if (store.HasComponent<PropertyComponent>(property.Parent))
        {
            outBusiness = null;
            return false;
        }
        
        VaultComponent? vault = null;
        
        //only get one for now. later we might add support for multiple banks
        foreach (var entity in store.Query(VaultComponent.GetComponentType()))
        {
            vault = store.GetComponent<VaultComponent>(entity);
            break;
        }

        if (vault == null)
        {
            outBusiness = null;
            return false;
        }
        
        outBusiness = commands.AddComponent(property.Parent, new BusinessComponent());

        outBusiness.Name = name;
        outBusiness.Owner = owner;
        outBusiness.Property = property;
        outBusiness.Inventory = commands.AddComponent(property.Parent, new InventoryComponent());
        
        vaultService.AddBankAccount(vault, out var businessAccount);
        outBusiness.PaymentAccount = businessAccount;
        return true;
    }
}