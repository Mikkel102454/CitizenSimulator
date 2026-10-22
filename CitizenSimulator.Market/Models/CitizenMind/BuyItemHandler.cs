using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Models.Abstractions;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Market.Services.Abstractions;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.Market.Models.CitizenMind;

public sealed class BuyItemHandler(IEntityStore store, IMarketService market, string itemId) : IMindActionHandler
{
    public ActionResult Start(ActionContext context)
    {
        return CanPurchase(context) ? ActionResult.RUNNING : ActionResult.FAILED;
    }

    public ActionResult Tick(ActionContext context)
    {
        if (!CanPurchase(context))
            return ActionResult.FAILED;

        if (context.Elapsed.TotalMinutes < context.GetParameter<double>("durationMinutes"))
            return ActionResult.RUNNING;

        var offer = ResolveOffer(context)!;
        var inventory = store.GetComponent<InventoryComponent>(context.Citizen)!;
        var account = store.GetComponent<BankAccountComponent>(context.Citizen)!;
        decimal amount = (decimal)context.Bindings["quantity"]!;
        return market.TryBuyOffer(offer, inventory, account, amount) == IMarketService.BuyStatus.SUCCESS
            ? ActionResult.SUCCEEDED : ActionResult.FAILED;
    }

    public void Cancel(ActionContext context)
    {
        // Stock and money change only at successful completion.
    }

    private bool CanPurchase(ActionContext context)
    {
        if (!store.IsAlive(context.Citizen)
            || store.GetComponent<CitizenComponent>(context.Citizen) is not { Alive: true }
            || store.GetComponent<InventoryComponent>(context.Citizen) is not { } inventory
            || store.GetComponent<BankAccountComponent>(context.Citizen) is not { } account
            || !context.Bindings.TryGetValue("quantity", out var amountValue) || amountValue is not decimal amount)
            return false;

        var offer = ResolveOffer(context);
        if (offer == null)
            return false;

        var status = market.CheckOffer(offer, inventory, account, amount);
        if (status != IMarketService.BuyStatus.SUCCESS)
        {
            market.ReportDemand(inventory, account, itemId, amount, offer.Seller, status);
            return false;
        }

        return true;
    }

    private MarketOffer? ResolveOffer(ActionContext context)
    {
        if (!context.Bindings.TryGetValue("seller", out var sellerValue) || sellerValue is not EntityRef sellerEntity
            || !store.IsAlive(sellerEntity)
            || store.GetComponent<BusinessComponent>(sellerEntity) is not { } seller
            || !context.Bindings.TryGetValue("unitPrice", out var priceValue) || priceValue is not decimal price
            || !context.Bindings.TryGetValue("availableQuantity", out var stockValue) || stockValue is not decimal stock)
            return null;

        return new MarketOffer(seller, itemId, price, stock);
    }
}
