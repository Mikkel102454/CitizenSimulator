using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Models;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Calender.Services.Abstractions;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Inventory.Services.Abstractions;
using CitizenSimulator.Items.Services.Abstractions;
using CitizenSimulator.Market.Services.Abstractions;
using CitizenSimulator.Property.Components;
using CitizenSimulator.Vault.Components;
using CitizenSimulator.Vault.Services.Abstractions;
using BuyStatus = CitizenSimulator.Market.Services.Abstractions.IMarketService.BuyStatus;

namespace CitizenSimulator.Market.Services;

public partial class MarketService(IEntityStore store,
    IEventBus events,
    IVaultService vaultService,
    IInventoryService inventoryService,
    IBusinessService businesses,
    IItemService items,
    ICalenderService calendar) : IMarketService
{
    private decimal GetQuantityStep(string itemId)
    {
        return items.GetItems().FirstOrDefault(item => item.Id == itemId)?.QuantityStep ?? 0;
    }

    private bool IsValidQuantity(string itemId, decimal amount)
    {
        decimal step = GetQuantityStep(itemId);
        return step > 0 && amount > 0 && amount % step == 0;
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
        return business != null && ReferenceEquals(business.Inventory, inventory)
            && ReferenceEquals(business.PaymentAccount, account) ? business : null;
    }

    private bool IsValidBuyer(InventoryComponent inventory, BankAccountComponent account)
    {
        if (!store.IsAlive(inventory.Parent) || inventory.Parent != account.Parent
            || !ReferenceEquals(store.GetComponent<InventoryComponent>(inventory.Parent), inventory)
            || !ReferenceEquals(store.GetComponent<BankAccountComponent>(account.Parent), account)
            || !account.Vault.Accounts.Contains(account))
            return false;

        var business = GetBuyerBusiness(inventory, account);
        if (business != null)
            return business.State is not (BusinessState.Bankrupt or BusinessState.ShutDown)
                && business.Owner.Alive && !business.Property.Destroyed;

        return store.GetComponent<CitizenComponent>(inventory.Parent) is { Alive: true };
    }

    private bool IsRegisteredSeller(BusinessComponent seller)
    {
        return store.IsAlive(seller.Parent)
            && ReferenceEquals(store.GetComponent<BusinessComponent>(seller.Parent), seller)
            && seller.PaymentAccount.Vault.Accounts.Contains(seller.PaymentAccount);
    }

    private BuyStatus GetSellerStatus(BusinessComponent seller, string itemId, bool wholesale)
    {
        string channel = wholesale ? "wholesale" : "retail";
        var policy = seller.Type.SalesPolicy;
        if (policy == null || !policy.Channels.Contains(channel) || !policy.SellItems.ContainsKey(itemId))
            return BuyStatus.ACCESS_DENIED;

        if (!IsRegisteredSeller(seller) || !seller.IsOperating || seller.Property.Destroyed || !seller.Owner.Alive
            || !store.IsAlive(seller.Property.Parent)
            || !ReferenceEquals(store.GetComponent<PropertyComponent>(seller.Property.Parent), seller.Property)
            || !ReferenceEquals(seller.Property.Owner, seller.Owner)
            || seller.Property.AreaM2 < seller.Type.Premises.MinimumAreaM2
            || !seller.Type.Premises.AllowedUses.Contains(seller.Property.Type.ToString(), StringComparer.OrdinalIgnoreCase))
            return BuyStatus.CLOSED_SELLER;

        if (NeedsCustomerCapacity(seller) && seller.AvailableCustomerCapacity < 1)
            return BuyStatus.NO_SERVICE_CAPACITY;

        return seller.Inventory.GetQuantity(itemId) > 0 ? BuyStatus.SUCCESS : BuyStatus.OUT_OF_STOCK;
    }

    private static bool NeedsCustomerCapacity(BusinessComponent seller)
    {
        return seller.Type.SalesPolicy?.CustomersServedPerWorkerHour > 0
            && seller.Type.Workforce.Any(requirement => requirement.DesiredCount > 0);
    }

    private static decimal GetSpendableBalance(BankAccountComponent account, BusinessComponent? buyer)
    {
        return Math.Max(0, account.Balance - (buyer?.UnpaidLiabilities ?? 0));
    }

    public IReadOnlyList<MarketOffer> GetOffers(string itemId, bool wholesale = false, bool includeUnavailable = false)
    {
        string channel = wholesale ? "wholesale" : "retail";
        var offers = new List<MarketOffer>();
        foreach (var entity in store.Query(BusinessComponent.GetComponentType()))
        {
            var seller = store.GetComponent<BusinessComponent>(entity)!;
            var policy = seller.Type.SalesPolicy;
            if (seller.State is BusinessState.Bankrupt or BusinessState.ShutDown
                || policy == null || !policy.Channels.Contains(channel) || !policy.SellItems.ContainsKey(itemId))
                continue;

            var status = GetSellerStatus(seller, itemId, wholesale);
            if (!includeUnavailable && status != BuyStatus.SUCCESS)
                continue;

            var windows = BusinessTime.GetWorkingWindows(seller.Type.OpeningHours, seller.Type.OpeningHours,
                Now, Now + TimeSpan.FromDays(8), calendar);
            var nextWindow = windows.Count == 0 ? ((TimeSpan Start, TimeSpan End)?)null : windows[0];
            offers.Add(new MarketOffer(seller, itemId, businesses.GetSalePrice(seller, itemId),
                seller.Inventory.GetQuantity(itemId), wholesale, status, nextWindow?.Start, nextWindow?.End));
        }

        return offers.OrderBy(offer => offer.UnitPrice).ThenBy(offer => offer.Seller.Parent.Id).ToArray();
    }

    public BuyStatus CheckOffer(MarketOffer offer, InventoryComponent inventory,
        BankAccountComponent account, decimal amount)
    {
        if (!IsValidQuantity(offer.ItemId, amount) || offer.UnitPrice < 0)
            return BuyStatus.INVALID_QUANTITY;

        if (!IsValidBuyer(inventory, account))
            return BuyStatus.INVALID_BUYER;

        var buyer = GetBuyerBusiness(inventory, account);
        var seller = offer.Seller;
        if (offer.Wholesale != (buyer != null) || ReferenceEquals(seller.Inventory, inventory)
            || ReferenceEquals(seller.PaymentAccount, account))
            return BuyStatus.ACCESS_DENIED;

        var status = GetSellerStatus(seller, offer.ItemId, buyer != null);
        if (status is BuyStatus.ACCESS_DENIED or BuyStatus.CLOSED_SELLER)
            return status;

        decimal currentPrice = businesses.GetSalePrice(seller, offer.ItemId);
        if (currentPrice > offer.UnitPrice)
            return BuyStatus.PRICE_CHANGED;

        if (currentPrice * amount > GetSpendableBalance(account, buyer))
            return BuyStatus.INSUFFICIENT_FUNDS;

        if (amount > inventory.FreeCapacity)
            return BuyStatus.INVENTORY_FULL;

        if (status != BuyStatus.SUCCESS)
            return status;

        if (seller.Inventory.GetQuantity(offer.ItemId) < amount || offer.AvailableQuantity < amount)
            return BuyStatus.OUT_OF_STOCK;

        return BuyStatus.SUCCESS;
    }

    public BuyStatus TryBuyOffer(MarketOffer offer, InventoryComponent inventory,
        BankAccountComponent account, decimal amount)
    {
        if (!IsValidQuantity(offer.ItemId, amount))
            return BuyStatus.INVALID_QUANTITY;

        if (!IsValidBuyer(inventory, account))
            return BuyStatus.INVALID_BUYER;

        ObserveDemand(inventory, account, offer.ItemId, amount, offer.Seller);
        var status = CommitPurchase(offer, inventory, account, amount);
        SetRequestStatus(inventory, offer.ItemId, status);
        return status;
    }
}
