using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.Market.Services.Abstractions;

namespace CitizenSimulator.Market.Systems;

public sealed class MarketSystem(IMarketService market, IBusinessService businesses) : GameSystem(priority: -4)
{
    protected override void OnUpdate(SimulationTime time, ICommandBuffer commands)
    {
        market.AdvanceMarket(businesses.GameTime);
    }
}
