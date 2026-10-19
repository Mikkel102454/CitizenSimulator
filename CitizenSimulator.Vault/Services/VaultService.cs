using CitizenSimulator.Vault.Services.Abstractions;
using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Vault.Services;

public class VaultService(IEntityStore store, IEventBus events) : IVaultService
{
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

    public bool AddBankAccount(VaultComponent vault, out VaultComponent.Account outAccount)
    {
        outAccount = new VaultComponent.Account();
        outAccount.Balance = 0;
        vault.Accounts.Add(outAccount);
        return true;
    }

    public bool RemoveBankAccount(VaultComponent vault, VaultComponent.Account account)
    {
        vault.Accounts.Remove(account);
        return true;
    }

    public bool AddBalance(VaultComponent.Account account, decimal amount)
    {
        account.Balance += amount;
        
        return true;
    }

    public bool RemoveBalance(VaultComponent.Account account, decimal amount)
    {
        account.Balance -= amount;
        
        return true;
    }

    public bool TransferBalance(VaultComponent.Account senderAccount, VaultComponent.Account receiverAccount, decimal amount)
    {
        if(senderAccount.Balance < amount)
            return false;
        
        senderAccount.Balance -= amount;
        receiverAccount.Balance += amount;
        return true;
    }

    public bool SetBalance(VaultComponent.Account account, decimal amount)
    {
        account.Balance = amount;
        
        return true;
    }
}