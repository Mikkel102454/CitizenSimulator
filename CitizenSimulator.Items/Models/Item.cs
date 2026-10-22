namespace CitizenSimulator.Items.Models;

public class Item
{
    internal static List<Item> ItemList { get; set; } = [];
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public bool Consumable { get; init; } = false;
    public decimal QuantityStep { get; init; } = 1;
    public decimal? MassKgPerUnit { get; init; }
    public List<Effect> Effects { get; init; } = [];
}
