using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Vault.Services.Abstractions;

public interface IVaultService
{
    bool CreateVault(ICommandBuffer commands, EntityRef entityRef, string name, double interestRate, out VaultComponent? outVault);
    bool DeleteVault(ICommandBuffer commands, VaultComponent vault);
    bool AddBankAccount(VaultComponent vault, CitizenComponent citizen);
    bool RemoveBankAccount(VaultComponent vault, VaultComponent.Account account);
    
    bool AddBalance(VaultComponent vault, VaultComponent.Account account, double amount);
    bool RemoveBalance(VaultComponent vault, VaultComponent.Account account, double amount);
    bool SetBalance(VaultComponent vault, VaultComponent.Account account, double amount);
    
}