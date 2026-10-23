using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Citizen.Events;
using CitizenSimulator.Vault.Components;
using CitizenSimulator.Vault.Services.Abstractions;

namespace CitizenSimulator.Vault.Systems;

public class VaultSystem(IEventBus events, IVaultService vaultService, IWalletService walletService) : GameSystem
{
    private IDisposable? _citizenCreated;
    private VaultComponent _primaryVault;

    protected override void OnStart(ICommandBuffer commands)
    {
        EntityRef vaultEntity = commands.CreateEntity();
        _primaryVault = commands.AddComponent(vaultEntity,  new VaultComponent());
        _citizenCreated = events.Subscribe<OnCitizenCreated>(OnCitizenCreated);
    }

    private void OnCitizenCreated(OnCitizenCreated eventData, SimulationTime time, ICommandBuffer commands)
    {
        vaultService.AddBankAccount(_primaryVault, out var account);
        commands.AddComponent(eventData.Citizen.Parent, account);
        commands.AddComponent(eventData.Citizen.Parent, new WalletComponent());

        vaultService.AddBalance(account, 20000);
    }
}
