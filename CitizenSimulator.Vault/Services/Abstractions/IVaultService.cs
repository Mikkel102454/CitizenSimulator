using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Vault.Services.Abstractions;

public interface IVaultService
{
    bool CreateVault(ICommandBuffer commands, EntityRef entityRef, string name, out VaultComponent? outVault);
    bool DeleteVault(ICommandBuffer commands, VaultComponent vault);
    bool AddBankAccount(VaultComponent vault, out BankAccountComponent outAccount);
    bool RemoveBankAccount(ICommandBuffer commands, BankAccountComponent account);
    
    bool AddBalance(BankAccountComponent account, decimal amount);
    bool RemoveBalance(BankAccountComponent account, decimal amount);
    bool TransferBalance(BankAccountComponent senderAccount, BankAccountComponent receiverAccount, decimal amount);
    bool SetBalance(BankAccountComponent account, decimal amount);
    
}