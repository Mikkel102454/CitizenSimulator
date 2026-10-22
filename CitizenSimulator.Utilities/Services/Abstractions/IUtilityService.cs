using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Services;
using CitizenSimulator.Utilities.Models;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Utilities.Services.Abstractions;

public interface IUtilityService : ICitySupplyService, IHomeUtilityService
{
    SupplyConfiguration Configuration { get; }
    BankAccountComponent? WorldAccount { get; }
    BankAccountComponent? StartupFund { get; }
    bool Connect(EntityRef business, decimal electricityKw, decimal waterLitresPerHour);
    bool Disconnect(EntityRef business);
    UtilityConnection? GetConnection(EntityRef business);
    bool ConnectHome(EntityRef house);
    bool DisconnectHome(EntityRef house);
    HomeUtilityConnection? GetHomeConnection(EntityRef house);
    void SupplyHomes();
    SupplyStatistics GetStatistics(string resourceId);
    void Advance(ulong tick, ICommandBuffer commands);
}
