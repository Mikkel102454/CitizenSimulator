using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Market.Models;
using CitizenSimulator.Market.Services.Abstractions;
using CitizenSimulator.Vault.Components;
using BuyStatus = CitizenSimulator.Market.Services.Abstractions.IMarketService.BuyStatus;

namespace CitizenSimulator.Market.Services;

public partial class MarketService
{
    private readonly Dictionary<(InventoryComponent Buyer, string ItemId), MarketRequest> _requests = [];
    private readonly List<MarketSale> _sales = [];
    private readonly List<MarketFailure> _failures = [];
    private readonly List<StockSample> _stockSamples = [];
    private TimeSpan _observedMarketTime;
    private TimeSpan? _lastPolicyUpdate;
    private TimeSpan Now => businesses.GameTime > _observedMarketTime ? businesses.GameTime : _observedMarketTime;

    public void ReportDemand(InventoryComponent inventory, BankAccountComponent account,
        string itemId, decimal amount, BusinessComponent? preferredSeller = null, BuyStatus? failureStatus = null)
    {
        ObserveDemand(inventory, account, itemId, amount, preferredSeller, recordFailure: true, failureStatus);
    }

    private void ObserveDemand(InventoryComponent inventory, BankAccountComponent account,
        string itemId, decimal amount, BusinessComponent? preferredSeller = null,
        bool recordFailure = false, BuyStatus? failureStatus = null)
    {
        if (!IsValidBuyer(inventory, account) || !IsValidQuantity(itemId, amount))
            return;

        bool wholesale = GetBuyerBusiness(inventory, account) != null;
        var listings = GetOffers(itemId, wholesale, includeUnavailable: true)
            .Where(offer => !ReferenceEquals(offer.Seller.Inventory, inventory));
        var selected = preferredSeller == null
            ? listings.OrderBy(offer => offer.Availability != BuyStatus.SUCCESS).ThenBy(offer => offer.UnitPrice).FirstOrDefault()
            : listings.FirstOrDefault(offer => ReferenceEquals(offer.Seller, preferredSeller));
        var status = selected == null
            ? preferredSeller == null ? BuyStatus.OUT_OF_STOCK : GetSellerStatus(preferredSeller, itemId, wholesale)
            : CheckOffer(selected, inventory, account, amount);
        status = failureStatus ?? status;

        var key = (inventory, itemId);
        if (!_requests.TryGetValue(key, out var request) || request.RemainingQuantity == 0
            || !ReferenceEquals(request.PaymentAccount, account)
            || Now - request.LastObservedAt > TimeSpan.FromDays(1))
        {
            request = new MarketRequest { Buyer = inventory, ItemId = itemId, PaymentAccount = account };
            _requests[key] = request;
        }

        request.Seller = selected?.Seller.Parent ?? preferredSeller?.Parent;
        request.RemainingQuantity = amount;
        request.LastObservedAt = Now;
        if (recordFailure)
            SetRequestStatus(inventory, itemId, status);
        else
            request.Status = status;
    }

    public void CancelDemand(InventoryComponent inventory, string itemId)
    {
        if (_requests.TryGetValue((inventory, itemId), out var request))
            request.RemainingQuantity = 0;
    }

    public void ResolveExternalDemand(InventoryComponent inventory, string itemId, decimal quantity)
    {
        if (quantity <= 0 || !_requests.TryGetValue((inventory, itemId), out var request))
            return;

        request.RemainingQuantity = Math.Max(0, request.RemainingQuantity - quantity);
        if (request.RemainingQuantity == 0)
            request.Status = BuyStatus.SUCCESS;
    }

    private BuyStatus GetRequestStatus(InventoryComponent inventory, string itemId)
    {
        return _requests.TryGetValue((inventory, itemId), out var request) ? request.Status : BuyStatus.OUT_OF_STOCK;
    }

    private void SetRequestStatus(InventoryComponent inventory, string itemId, BuyStatus status)
    {
        if (!_requests.TryGetValue((inventory, itemId), out var request))
            return;

        request.Status = status;
        if (status == BuyStatus.SUCCESS || request.RemainingQuantity <= request.LargestFailedQuantity)
            return;

        decimal newFailedQuantity = request.RemainingQuantity - request.LargestFailedQuantity;
        _failures.Add(new MarketFailure(Now, request.Seller, itemId, newFailedQuantity, status));
        request.LargestFailedQuantity = request.RemainingQuantity;
    }

    private void RecordSale(BusinessComponent seller, InventoryComponent buyer, string itemId, decimal amount, decimal revenue)
    {
        _sales.Add(new MarketSale(Now, seller.Parent, itemId, amount, revenue));
        if (_requests.TryGetValue((buyer, itemId), out var request))
            request.RemainingQuantity = Math.Max(0, request.RemainingQuantity - amount);
    }

    private void RefreshRequest(MarketRequest request)
    {
        if (!IsValidBuyer(request.Buyer, request.PaymentAccount))
        {
            request.RemainingQuantity = 0;
            return;
        }

        bool wholesale = GetBuyerBusiness(request.Buyer, request.PaymentAccount) != null;
        BusinessComponent? seller = null;
        if (request.Seller is { } sellerEntity && store.IsAlive(sellerEntity))
            seller = store.GetComponent<BusinessComponent>(sellerEntity);

        if (seller == null)
        {
            var listing = GetOffers(request.ItemId, wholesale, includeUnavailable: true)
                .FirstOrDefault(offer => !ReferenceEquals(offer.Seller.Inventory, request.Buyer));
            seller = listing?.Seller;
            request.Seller = seller?.Parent;
        }

        if (seller == null)
        {
            request.Status = BuyStatus.OUT_OF_STOCK;
            return;
        }

        if (seller.Type.SalesPolicy?.SellItems.ContainsKey(request.ItemId) != true)
        {
            request.Status = BuyStatus.ACCESS_DENIED;
            return;
        }

        var quote = new MarketOffer(seller, request.ItemId, businesses.GetSalePrice(seller, request.ItemId),
            seller.Inventory.GetQuantity(request.ItemId), wholesale);
        request.Status = CheckOffer(quote, request.Buyer, request.PaymentAccount, request.RemainingQuantity);
    }

    public MarketStatistics GetStatistics(string itemId, BusinessComponent? seller = null)
    {
        var cutoff = Now - TimeSpan.FromDays(1);
        EntityRef? sellerId = seller?.Parent;
        var sales = _sales.Where(sale => sale.Time > cutoff && sale.ItemId == itemId
            && (sellerId == null || sale.Seller == sellerId)).ToArray();
        var requests = _requests.Values.Where(request => request.LastObservedAt > cutoff
            && request.ItemId == itemId && (sellerId == null || request.Seller == sellerId)).ToArray();
        var samples = _stockSamples.Where(sample => sample.Time > cutoff && sample.ItemId == itemId
            && (sellerId == null || sample.Seller == sellerId));
        var sampledTotals = samples.GroupBy(sample => sample.Time).Select(group => group.Sum(sample => sample.Quantity)).ToArray();
        decimal stock = seller == null
            ? GetListedBusinesses(itemId).Sum(business => business.Inventory.GetQuantity(itemId))
            : seller.Inventory.GetQuantity(itemId);
        decimal averageStock = sampledTotals.Length == 0 ? stock : sampledTotals.Average();
        var failures = _failures.Where(failure => failure.Time > cutoff && failure.ItemId == itemId
            && (sellerId == null || failure.Seller == sellerId));

        return new MarketStatistics(cutoff < TimeSpan.Zero ? TimeSpan.Zero : cutoff, Now,
            sales.Sum(sale => sale.Quantity), sales.Sum(sale => sale.Revenue),
            requests.Sum(request => request.RemainingQuantity),
            requests.Where(request => request.Status != BuyStatus.SUCCESS).Sum(request => request.RemainingQuantity),
            requests.Where(request => request.Status == BuyStatus.OUT_OF_STOCK).Sum(request => request.RemainingQuantity),
            stock, averageStock)
        {
            Failures = failures.GroupBy(failure => failure.Status)
                .ToDictionary(group => group.Key, group => group.Sum(failure => failure.Quantity))
        };
    }

    private IEnumerable<BusinessComponent> GetListedBusinesses(string itemId)
    {
        return store.Query(BusinessComponent.GetComponentType())
            .Select(entity => store.GetComponent<BusinessComponent>(entity)!)
            .Where(business => business.Type.SalesPolicy?.SellItems.ContainsKey(itemId) == true);
    }
}
