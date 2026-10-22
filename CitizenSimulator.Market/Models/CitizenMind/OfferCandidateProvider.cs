using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Planning;
using CitizenSimulator.CitizenMind.Planning.Abstractions;
using CitizenSimulator.Market.Services.Abstractions;

namespace CitizenSimulator.Market.Models.CitizenMind;

public sealed class OfferCandidateProvider(IReadOnlyList<MarketOffer> offers, decimal quantity) : IActionCandidateProvider
{
    public IReadOnlyList<ActionCandidate> GetCandidates(ActionDefinition definition, EvaluationContext state)
    {
        var candidates = new List<ActionCandidate>();
        foreach (var offer in offers)
        {
            if (offer.Availability != IMarketService.BuyStatus.SUCCESS || offer.AvailableQuantity < quantity)
                continue;

            IReadOnlyList<TimeWindow>? availability = null;
            if (offer.AvailableFrom is { } start && offer.AvailableUntil is { } end)
            {
                // A transaction must finish before the exclusive closing boundary.
                end -= TimeSpan.FromTicks(1);
                if (end <= start)
                    continue;

                availability = [new TimeWindow(start, end)];
            }

            candidates.Add(new ActionCandidate
            {
                Id = $"{definition.Id}:{offer.Seller.Parent.Id}:{offer.Seller.Parent.Generation}:{offer.UnitPrice}",
                Definition = definition,
                Bindings = new Dictionary<string, object?>
                {
                    ["seller"] = offer.Seller.Parent,
                    ["unitPrice"] = offer.UnitPrice,
                    ["quantity"] = quantity,
                    ["availableQuantity"] = offer.AvailableQuantity
                },
                Availability = availability
            });
        }

        return candidates;
    }
}
