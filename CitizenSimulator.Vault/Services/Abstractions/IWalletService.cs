using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Vault.Services.Abstractions;

public interface IWalletService
{
    bool CreateWallet(ICommandBuffer commands, EntityRef entityRef, out WalletComponent? outWallet);
    bool DeleteWallet(ICommandBuffer commands, WalletComponent wallet);
    
    bool AddBalance(WalletComponent wallet, decimal amount);
    bool RemoveBalance(WalletComponent wallet, decimal amount);
    bool SetBalance(WalletComponent wallet, decimal amount);
}