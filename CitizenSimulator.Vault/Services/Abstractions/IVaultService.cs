using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Vault.Services.Abstractions;

public interface IVaultService
{
    bool CreateVault(ICommandBuffer commands, EntityRef entityRef, string name, out VaultComponent? outVault);
    bool DeleteVault(ICommandBuffer commands, VaultComponent vault);
    bool AddBankAccount(VaultComponent vault, out VaultComponent.Account outAccount);
    bool RemoveBankAccount(VaultComponent vault, VaultComponent.Account account);
    
    bool AddBalance(VaultComponent.Account account, decimal amount);
    bool RemoveBalance(VaultComponent.Account account, decimal amount);
    bool TransferBalance(VaultComponent.Account senderAccount, VaultComponent.Account receiverAccount, decimal amount);
    bool SetBalance(VaultComponent.Account account, decimal amount);
    
}