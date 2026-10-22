using CitizenSimulator.CitizenMind.Evaluations;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Planning;
using CitizenSimulator.CitizenMind.Planning.Abstractions;

namespace CitizenSimulator.Housing.Models.CitizenMind;

public sealed class HousingCandidateProvider(IReadOnlyList<HousingChoice> choices) : IActionCandidateProvider
{
    private readonly HousingChoice[] _choices = choices.ToArray();

    public IReadOnlyList<ActionCandidate> GetCandidates(ActionDefinition definition, EvaluationContext state)
    {
        if (Convert.ToDouble(state.Facts.GetValueOrDefault(HousingActions.HasHomeFact) ?? 0) > 0)
            return [];

        bool purchase = definition.Id == HousingActions.BuyAction;
        return _choices.Where(choice => choice.Purchase == purchase).Select(choice => new ActionCandidate
        {
            Id = $"{definition.Id}:{choice.Home.Id}:{choice.Home.Generation}:{choice.ListingId:N}",
            Definition = definition,
            Bindings = new Dictionary<string, object?>
            {
                ["home"] = choice.Home,
                ["listingId"] = choice.ListingId,
                ["price"] = choice.Price,
                ["canSleep"] = choice.CanSleep,
                ["canDrink"] = choice.CanDrink,
                ["canCook"] = choice.CanCook,
                ["canStore"] = choice.CanStore
            }
        }).ToArray();
    }
}
