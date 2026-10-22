using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Events;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.Business.Models;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Inventory.Services.Abstractions;
using CitizenSimulator.Items.Services.Abstractions;
using CitizenSimulator.Market.Services.Abstractions;
using CitizenSimulator.Vault.Components;
using CitizenSimulator.Vault.Services.Abstractions;

namespace CitizenSimulator.Market.Services;

public class MarketService(IEntityStore store,
    IEventBus events,
    IVaultService vaultService,
    IInventoryService inventoryService,
    IBusinessService businesses,
    IItemService items) : IMarketService
{
    private decimal GetQuantityStep(string itemId)
    {
        return items.GetItems().FirstOrDefault(item => item.Id == itemId)?.QuantityStep ?? 0;
    }

    private static decimal RoundDownToStep(decimal amount, decimal step)
    {
        return Math.Floor(amount / step) * step;
    }

    private BusinessComponent? GetBuyerBusiness(InventoryComponent inventory, BankAccountComponent account)
    {
        if (!store.IsAlive(inventory.Parent))
            return null;

        var business = store.GetComponent<BusinessComponent>(inventory.Parent);
        if (business == null || !ReferenceEquals(business.Inventory, inventory) || !ReferenceEquals(business.PaymentAccount, account))
            return null;

        return business;
    }

    public IReadOnlyList<MarketOffer> GetOffers(string itemId, bool wholesale = false)
    {
        string channel = wholesale ? "wholesale" : "retail";
        return store.Query(BusinessComponent.GetComponentType())
            .Select(entity => store.GetComponent<BusinessComponent>(entity)!)
            .Where(business => business.IsOperating
                && business.Type.SalesPolicy is { } policy
                && policy.Channels.Contains(channel)
                && policy.SellItems.ContainsKey(itemId)
                && business.Inventory.GetQuantity(itemId) > 0)
            .Select(business => new MarketOffer(
                business,
                itemId,
                businesses.GetSalePrice(business, itemId),
                business.Inventory.GetQuantity(itemId)))
            .OrderBy(offer => offer.UnitPrice)
            .ThenBy(offer => offer.Seller.Parent.Id)
            .ToArray();
    }

    private IMarketService.BuyStatus CommitPurchase(
        MarketOffer offer,
        InventoryComponent receiver,
        BankAccountComponent account,
        BusinessComponent? buyer,
        decimal amount,
        bool liquidation = false)
    {
        var seller = offer.Seller;
        bool isSelfPurchase = ReferenceEquals(seller.Inventory, receiver)
            || ReferenceEquals(seller.PaymentAccount, account);
        bool sellerUnavailable = !liquidation && !seller.IsOperating;
        bool insufficientStock = seller.Inventory.GetQuantity(offer.ItemId) < amount;
        if (isSelfPurchase || sellerUnavailable || insufficientStock)
            return IMarketService.BuyStatus.OUT_OF_STOCK;

        if (amount > receiver.FreeCapacity)
            return IMarketService.BuyStatus.INVENTORY_FULL;

        bool needsCustomer = !liquidation
            && seller.Type.SalesPolicy!.CustomersServedPerWorkerHour > 0
            && seller.Type.Workforce.Any(requirement => requirement.DesiredCount > 0);
        if (needsCustomer && seller.AvailableCustomerCapacity < 1)
            return IMarketService.BuyStatus.NO_SERVICE_CAPACITY;

        decimal unitPrice = liquidation ? offer.UnitPrice : businesses.GetSalePrice(seller, offer.ItemId);
        decimal price = unitPrice * amount;
        decimal spendable = Math.Max(0, account.Balance - (buyer?.UnpaidLiabilities ?? 0));
        if (price > spendable)
            return IMarketService.BuyStatus.INSUFFICIENT_FUNDS;

        // Precompute ledger arithmetic before committing money or stock.
        decimal costBasis = seller.Inventory.GetValue(offer.ItemId) * (amount / seller.Inventory.GetQuantity(offer.ItemId));
        _ = seller.SalesRevenue + price;
        _ = seller.CostOfGoodsSold + costBasis;
        _ = receiver.GetQuantity(offer.ItemId) + amount;
        _ = receiver.GetValue(offer.ItemId) + price;
        if (buyer != null)
            _ = buyer.StockPurchases + price;

        decimal buyerBalanceBefore = account.Balance;
        decimal sellerBalanceBefore = seller.PaymentAccount.Balance;
        if (price > 0 && !vaultService.TransferBalance(account, seller.PaymentAccount, price))
            return IMarketService.BuyStatus.INSUFFICIENT_FUNDS;

        if (!inventoryService.TryTransfer(offer.ItemId, seller.Inventory, receiver, amount, price, out var removedValue))
        {
            vaultService.SetBalance(account, buyerBalanceBefore);
            vaultService.SetBalance(seller.PaymentAccount, sellerBalanceBefore);
            return IMarketService.BuyStatus.INVENTORY_FULL;
        }

        if (needsCustomer)
            businesses.TryUseCustomerCapacity(seller);

        businesses.RecordTrade(seller, buyer, price, removedValue);
        events.Publish(new OnBusinessSale(seller, offer.ItemId, amount, price, buyer));
        return IMarketService.BuyStatus.SUCCESS;
    }

    public IMarketService.BuyStatus TryBuyLiquidationStock(
        BusinessComponent seller,
        InventoryComponent inventory,
        BankAccountComponent account,
        string itemId,
        decimal amount,
        decimal unitPrice)
    {
        decimal step = GetQuantityStep(itemId);
        if (step <= 0 || amount <= 0 || amount % step != 0 || unitPrice < 0 || !store.IsAlive(seller.Parent)
            || !ReferenceEquals(store.GetComponent<BusinessComponent>(seller.Parent), seller)
            || seller.State is not (BusinessState.Bankrupt or BusinessState.ShutDown))
            return IMarketService.BuyStatus.INVALID_QUANTITY;

        var offer = new MarketOffer(seller, itemId, unitPrice, seller.Inventory.GetQuantity(itemId));
        var buyer = GetBuyerBusiness(inventory, account);
        return CommitPurchase(offer, inventory, account, buyer, amount, liquidation: true);
    }

    public IMarketService.BuyStatus TryBuy(InventoryComponent inventory, BankAccountComponent account, string itemId, decimal amount)
    {
        decimal step = GetQuantityStep(itemId);
        if (step <= 0 || amount <= 0 || amount % step != 0)
            return IMarketService.BuyStatus.INVALID_QUANTITY;

        var buyer = GetBuyerBusiness(inventory, account);
        var result = IMarketService.BuyStatus.OUT_OF_STOCK;
        foreach (var offer in GetOffers(itemId, buyer != null))
        {
            if (offer.AvailableQuantity < amount || ReferenceEquals(offer.Seller.Inventory, inventory))
                continue;

            result = CommitPurchase(offer, inventory, account, buyer, amount);
            if (result == IMarketService.BuyStatus.SUCCESS || result == IMarketService.BuyStatus.INVENTORY_FULL)
                return result;

        }

        return result;
    }

    public PurchaseResult TryBuyUpTo(InventoryComponent inventory, BankAccountComponent account, string itemId, decimal amount)
    {
        decimal step = GetQuantityStep(itemId);
        if (step <= 0 || amount < step)
            return new(IMarketService.BuyStatus.INVALID_QUANTITY, 0, 0);

        var buyer = GetBuyerBusiness(inventory, account);
        decimal purchasedQuantity = 0;
        decimal totalSpent = 0;
        var status = IMarketService.BuyStatus.OUT_OF_STOCK;
        foreach (var offer in GetOffers(itemId, buyer != null))
        {
            if (ReferenceEquals(offer.Seller.Inventory, inventory))
                continue;

            decimal remainingQuantity = amount - purchasedQuantity;
            decimal availableQuantity = Math.Min(offer.AvailableQuantity, inventory.FreeCapacity);
            decimal quantity = Math.Min(remainingQuantity, availableQuantity);
            if (offer.UnitPrice > 0)
            {
                decimal spendableBalance = Math.Max(0, account.Balance - (buyer?.UnpaidLiabilities ?? 0));
                decimal affordableQuantity = spendableBalance / offer.UnitPrice;
                quantity = Math.Min(quantity, affordableQuantity);
            }

            quantity = RoundDownToStep(quantity, step);
            if (quantity <= 0)
                continue;

            status = CommitPurchase(offer, inventory, account, buyer, quantity);
            if (status == IMarketService.BuyStatus.SUCCESS)
            {
                purchasedQuantity += quantity;
                totalSpent += offer.UnitPrice * quantity;
            }

            if (amount - purchasedQuantity < step || inventory.FreeCapacity < step)
                break;

        }

        return new(purchasedQuantity > 0 ? IMarketService.BuyStatus.SUCCESS : status, purchasedQuantity, totalSpent);
    }
}
