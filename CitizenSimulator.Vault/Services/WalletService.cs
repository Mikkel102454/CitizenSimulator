using CitizenSimulator.Vault.Services.Abstractions;
using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Vault.Services;

public class WalletService(IEntityStore store, IEventBus events) : IWalletService
{
    public bool CreateWallet(ICommandBuffer commands, EntityRef entityRef, out WalletComponent? outWallet)
    {
        if (store.HasComponent<WalletComponent>(entityRef))
        {
            outWallet = null;
            return false;
        }
        
        outWallet = commands.AddComponent(entityRef, new WalletComponent());
        return true;
    }

    public bool DeleteWallet(ICommandBuffer commands, WalletComponent wallet)
    {
        commands.RemoveComponent<WalletComponent>(wallet.Parent);
        return true;
    }

    public bool AddBalance(WalletComponent wallet, double amount)
    {
        wallet.Money += amount;
        return true;
    }

    public bool RemoveBalance(WalletComponent wallet, double amount)
    {
        wallet.Money -= amount;
        return true;
    }

    public bool SetBalance(WalletComponent wallet, double amount)
    {
        wallet.Money = amount;
        return true;
    }
}