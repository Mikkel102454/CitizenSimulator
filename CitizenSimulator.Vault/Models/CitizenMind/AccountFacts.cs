using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.CitizenMind.Services.Abstractions;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Vault.Models.CitizenMind;

public static class AccountFacts
{
    // Retain the existing ID; shopping and housing must predict the same wallet.
    public const string Balance = "market.balance";

    public static void Register(IModContext context)
    {
        context.Services.Get<ICitizenMindService>().RegisterFact(Balance,
            entity => context.Store.IsAlive(entity)
                ? context.Store.GetComponent<BankAccountComponent>(entity)?.Balance ?? 0m : 0m, canPredict: true);
    }
}
