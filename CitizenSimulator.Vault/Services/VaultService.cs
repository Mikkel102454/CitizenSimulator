using CitizenSimulator.Vault.Services.Abstractions;
using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Vault.Services;

public class VaultService(IEntityStore store, IEventBus events) : IVaultService
{
    private uint nextAccountId = 1;
    
    public bool CreateVault(ICommandBuffer commands, EntityRef entityRef, string name, out VaultComponent? outVault)
    {
        if (store.HasComponent<VaultComponent>(entityRef))
        {
            outVault = null;
            return false;
        }
        
        outVault = commands.AddComponent(entityRef, new VaultComponent());
        outVault.Name = name;

        return true;
    }

    public bool DeleteVault(ICommandBuffer commands, VaultComponent vault)
    {
        commands.RemoveComponent<VaultComponent>(vault.Parent);
        return true;
    }

    public bool AddBankAccount(VaultComponent vault, CitizenComponent citizen)
    {
        VaultComponent.Account account = new VaultComponent.Account();
        account.Balance = 0;
        account.Citizen = citizen;
        vault.Accounts.Add(account);
        return true;
    }

    public bool RemoveBankAccount(VaultComponent vault, VaultComponent.Account account)
    {
        vault.Accounts.Remove(account);
        return true;
    }

    public bool AddBalance(VaultComponent vault, VaultComponent.Account account, double amount)
    {
        int index = vault.Accounts.FindIndex(a => a == account);
        
        if (index == -1) return false;
        
        var targetAccount = vault.Accounts[index];
        targetAccount.Balance += amount;
        vault.Accounts[index] = targetAccount;
        
        return true;
    }

    public bool RemoveBalance(VaultComponent vault, VaultComponent.Account account, double amount)
    {
        int index = vault.Accounts.FindIndex(a => a == account);
        
        if (index == -1) return false;
        
        var targetAccount = vault.Accounts[index];
        targetAccount.Balance -= amount;
        vault.Accounts[index] = targetAccount;
        
        return true;
    }

    public bool SetBalance(VaultComponent vault, VaultComponent.Account account, double amount)
    {
        int index = vault.Accounts.FindIndex(a => a == account);
        
        if (index == -1) return false;
        
        var targetAccount = vault.Accounts[index];
        targetAccount.Balance = amount;
        vault.Accounts[index] = targetAccount;
        
        return true;
    }
}