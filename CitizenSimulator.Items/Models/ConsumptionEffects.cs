using System.Text.Json;

namespace CitizenSimulator.Items.Models;

/// <summary>Normalized per-portion changes shared by execution and planning.</summary>
public sealed record ConsumptionEffects(double Hunger, double Thirst, double Energy)
{
    public static bool TryRead(Item item, out ConsumptionEffects effects)
    {
        effects = new(0, 0, 0);
        if (!item.Consumable) return false;
        double hunger = 0, thirst = 0, energy = 0;
        foreach (var effect in item.Effects)
        {
            if (effect.Type != "needs.modify" ||
                !effect.Values.TryGetValue("need", out var need) || need.ValueKind != JsonValueKind.String ||
                !effect.Values.TryGetValue("amount", out var amount) || amount.ValueKind != JsonValueKind.Number ||
                !amount.TryGetDouble(out var change) || !double.IsFinite(change)) return false;

            switch (need.GetString())
            {
                case "hunger": hunger += change; break;
                case "thirst": thirst += change; break;
                case "energy": energy += change; break;
                default: return false;
            }
        }
        if (!double.IsFinite(hunger) || !double.IsFinite(thirst) || !double.IsFinite(energy)) return false;
        effects = new(hunger, thirst, energy);
        return true;
    }
}
