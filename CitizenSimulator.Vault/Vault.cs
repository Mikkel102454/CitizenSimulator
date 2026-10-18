using CitizenSimulator.Vault.Services.Abstractions;
using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Vault.Services;
using CitizenSimulator.Vault.Systems;

namespace CitizenSimulator.Vault;

[Mod("vault", "Vault", "1.0.0", Dependencies = new[] { "calender" })]
public class Vault : IMod
{
    public void OnLoad(IModContext context)
    {
        context.Services.Register<IVaultService>(new VaultService(context.Store, context.Events));
        context.Services.Register<IWalletService>(new WalletService(context.Store, context.Events));
        context.Systems.Register(new VaultSystem(context.Events));
    }
}