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

    public bool AddBankAccount(VaultComponent vault, out BankAccountComponent outAccount)
    {
        outAccount = new BankAccountComponent()
        {
            Vault = vault
        };
        outAccount.Balance = 0;
        vault.Accounts.Add(outAccount);
        return true;
    }

    public bool RemoveBankAccount(ICommandBuffer commands, BankAccountComponent account)
    {
        account.Vault.Accounts.Remove(account);
        commands.RemoveComponent<BankAccountComponent>(account.Parent);
        return true;
    }

    public bool AddBalance(BankAccountComponent account, decimal amount)
    {
        if (amount <= 0 || account.Balance > decimal.MaxValue - amount)
            return false;

        account.Balance += amount;
        return true;
    }

    public bool RemoveBalance(BankAccountComponent account, decimal amount)
    {
        if (amount <= 0 || account.Balance < amount)
            return false;

        account.Balance -= amount;
        return true;
    }

    public bool TransferBalance(BankAccountComponent senderAccount,
        BankAccountComponent receiverAccount,
        decimal amount)
    {
        if (amount <= 0
            || ReferenceEquals(senderAccount, receiverAccount)
            || senderAccount.Balance < amount
            || receiverAccount.Balance > decimal.MaxValue - amount
            || !senderAccount.Vault.Accounts.Contains(senderAccount)
            || !receiverAccount.Vault.Accounts.Contains(receiverAccount))
            return false;

        senderAccount.Balance -= amount;
        receiverAccount.Balance += amount;
        return true;
    }

    public bool SetBalance(BankAccountComponent account, decimal amount)
    {
        if (amount < 0)
            return false;

        account.Balance = amount;
        return true;
    }
}
