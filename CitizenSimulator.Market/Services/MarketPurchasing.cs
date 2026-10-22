using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Events;
using CitizenSimulator.Business.Models;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Market.Services.Abstractions;
using CitizenSimulator.Vault.Components;
using BuyStatus = CitizenSimulator.Market.Services.Abstractions.IMarketService.BuyStatus;

namespace CitizenSimulator.Market.Services;

public partial class MarketService
{
    private BuyStatus CommitPurchase(MarketOffer offer, InventoryComponent receiver,
        BankAccountComponent account, decimal amount, bool liquidation = false)
    {
        var buyer = GetBuyerBusiness(receiver, account);
        var seller = offer.Seller;
        var status = liquidation
            ? CheckLiquidationPurchase(offer, receiver, account, amount)
            : CheckOffer(offer, receiver, account, amount);
        if (status != BuyStatus.SUCCESS)
            return status;

        decimal unitPrice = liquidation ? offer.UnitPrice : businesses.GetSalePrice(seller, offer.ItemId);
        decimal price = unitPrice * amount;
        decimal costBasis = seller.Inventory.GetValue(offer.ItemId) * (amount / seller.Inventory.GetQuantity(offer.ItemId));

        // Stage ledger arithmetic before either bank or inventory changes.
        _ = costBasis / amount;
        _ = seller.SalesRevenue + price;
        _ = seller.CostOfGoodsSold + costBasis;
        _ = receiver.GetQuantity(offer.ItemId) + amount;
        _ = receiver.GetValue(offer.ItemId) + price;
        if (buyer != null)
            _ = buyer.StockPurchases + price;

        decimal buyerBalanceBefore = account.Balance;
        decimal sellerBalanceBefore = seller.PaymentAccount.Balance;
        if (price > 0 && !vaultService.TransferBalance(account, seller.PaymentAccount, price))
            return BuyStatus.INSUFFICIENT_FUNDS;

        if (!inventoryService.TryTransfer(offer.ItemId, seller.Inventory, receiver, amount, price, out var removedValue))
        {
            vaultService.SetBalance(account, buyerBalanceBefore);
            vaultService.SetBalance(seller.PaymentAccount, sellerBalanceBefore);
            return BuyStatus.INVENTORY_FULL;
        }

        if (!liquidation && NeedsCustomerCapacity(seller))
            businesses.TryUseCustomerCapacity(seller);

        businesses.RecordTrade(seller, buyer, price, removedValue, offer.ItemId, amount);
        if (!liquidation)
            RecordSale(seller, receiver, offer.ItemId, amount, price);

        events.Publish(new OnBusinessSale(seller, offer.ItemId, amount, price, buyer));
        return BuyStatus.SUCCESS;
    }

    private BuyStatus CheckLiquidationPurchase(MarketOffer offer, InventoryComponent inventory,
        BankAccountComponent account, decimal amount)
    {
        var seller = offer.Seller;
        if (!IsRegisteredSeller(seller) || seller.State is not (BusinessState.Bankrupt or BusinessState.ShutDown)
            || ReferenceEquals(seller.Inventory, inventory) || ReferenceEquals(seller.PaymentAccount, account))
            return BuyStatus.ACCESS_DENIED;

        if (seller.Inventory.GetQuantity(offer.ItemId) < amount)
            return BuyStatus.OUT_OF_STOCK;

        if (amount > inventory.FreeCapacity)
            return BuyStatus.INVENTORY_FULL;

        var buyer = GetBuyerBusiness(inventory, account);
        return offer.UnitPrice * amount > GetSpendableBalance(account, buyer)
            ? BuyStatus.INSUFFICIENT_FUNDS : BuyStatus.SUCCESS;
    }

    public BuyStatus TryBuyLiquidationStock(BusinessComponent seller, InventoryComponent inventory,
        BankAccountComponent account, string itemId, decimal amount, decimal unitPrice)
    {
        if (!IsValidQuantity(itemId, amount) || unitPrice < 0)
            return BuyStatus.INVALID_QUANTITY;

        if (!IsValidBuyer(inventory, account))
            return BuyStatus.INVALID_BUYER;

        var offer = new MarketOffer(seller, itemId, unitPrice, seller.Inventory.GetQuantity(itemId));
        return CommitPurchase(offer, inventory, account, amount, liquidation: true);
    }

    public BuyStatus TryBuy(InventoryComponent inventory, BankAccountComponent account, string itemId, decimal amount)
    {
        if (!IsValidQuantity(itemId, amount))
            return BuyStatus.INVALID_QUANTITY;

        if (!IsValidBuyer(inventory, account))
            return BuyStatus.INVALID_BUYER;

        ObserveDemand(inventory, account, itemId, amount);
        bool wholesale = GetBuyerBusiness(inventory, account) != null;
        var offers = GetOffers(itemId, wholesale);
        var result = GetRequestStatus(inventory, itemId);
        foreach (var offer in offers)
        {
            if (ReferenceEquals(offer.Seller.Inventory, inventory))
                continue;

            if (offer.AvailableQuantity < amount)
            {
                result = BuyStatus.OUT_OF_STOCK;
                continue;
            }

            result = CommitPurchase(offer, inventory, account, amount);
            if (result is BuyStatus.SUCCESS or BuyStatus.INVENTORY_FULL)
                break;
        }

        SetRequestStatus(inventory, itemId, result);
        return result;
    }

    public PurchaseResult TryBuyUpTo(InventoryComponent inventory, BankAccountComponent account, string itemId, decimal amount)
    {
        decimal step = GetQuantityStep(itemId);
        if (step <= 0 || amount < step)
            return new PurchaseResult(BuyStatus.INVALID_QUANTITY, 0, 0);

        if (!IsValidBuyer(inventory, account))
            return new PurchaseResult(BuyStatus.INVALID_BUYER, 0, 0);

        amount = RoundDownToStep(amount, step);
        ObserveDemand(inventory, account, itemId, amount);
        var buyer = GetBuyerBusiness(inventory, account);
        decimal purchased = 0;
        decimal spent = 0;
        var status = GetRequestStatus(inventory, itemId);
        foreach (var offer in GetOffers(itemId, buyer != null))
        {
            if (ReferenceEquals(offer.Seller.Inventory, inventory))
                continue;

            decimal remaining = amount - purchased;
            decimal available = Math.Min(offer.AvailableQuantity, inventory.FreeCapacity);
            decimal quantity = Math.Min(remaining, available);
            if (offer.UnitPrice > 0)
                quantity = Math.Min(quantity, GetSpendableBalance(account, buyer) / offer.UnitPrice);

            quantity = RoundDownToStep(quantity, step);
            if (quantity <= 0)
                continue;

            decimal unitPrice = businesses.GetSalePrice(offer.Seller, itemId);
            status = CommitPurchase(offer, inventory, account, quantity);
            if (status == BuyStatus.SUCCESS)
            {
                purchased += quantity;
                spent += unitPrice * quantity;
            }

            if (amount - purchased < step || inventory.FreeCapacity < step)
                break;
        }

        if (purchased < amount)
        {
            ReportDemand(inventory, account, itemId, amount - purchased);
            status = GetRequestStatus(inventory, itemId);
        }

        return new PurchaseResult(purchased > 0 ? BuyStatus.SUCCESS : status, purchased, spent);
    }
}
