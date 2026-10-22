using CitizenSimulator.Citizen.Components;
using CitizenSimulator.Housing.Models;
using CitizenSimulator.Inventory.Components;

namespace CitizenSimulator.Housing.Services;

public sealed partial class HousingService
{
    public IReadOnlyList<HomeStorage> GetStorage(CitizenComponent citizen) => _storage
        .Where(storage => ReferenceEquals(storage.Owner, citizen)).ToArray();

    public bool TryStore(CitizenComponent citizen, string itemId, decimal quantity)
    {
        if (!IsLiving(citizen) || string.IsNullOrWhiteSpace(itemId) || quantity <= 0
            || GetAccessibleHome(citizen.Parent) is not { } house
            || store.GetComponent<InventoryComponent>(citizen.Parent) is not { } carried)
            return false;

        decimal used = _storage.Where(storage => ReferenceEquals(storage.House, house)).Sum(storage => storage.UsedCapacity);
        if (quantity > house.StorageCapacity - used)
            return false;

        HomeStorage? existing = _storage.FirstOrDefault(storage => ReferenceEquals(storage.House, house)
            && ReferenceEquals(storage.Owner, citizen));
        HomeStorage destination = existing ?? new HomeStorage { Owner = citizen, House = house };
        if (!TransferBelongings(carried, destination.Inventory, itemId, quantity))
            return false;

        if (existing == null)
            _storage.Add(destination);

        return true;
    }

    public bool TryRetrieve(CitizenComponent citizen, HomeStorage storage, string itemId, decimal quantity)
    {
        if (!IsLiving(citizen) || string.IsNullOrWhiteSpace(itemId)
            || !_storage.Contains(storage) || !ReferenceEquals(storage.Owner, citizen)
            || quantity <= 0 || store.GetComponent<InventoryComponent>(citizen.Parent) is not { } carried)
            return false;

        return TransferBelongings(storage.Inventory, carried, itemId, quantity);
    }

    private bool TransferBelongings(InventoryComponent sender, InventoryComponent receiver,
        string itemId, decimal quantity)
    {
        decimal available = sender.GetQuantity(itemId);
        if (quantity <= 0 || available < quantity)
            return false;

        decimal value = sender.GetValue(itemId) * (quantity / available);
        if (receiver.GetQuantity(itemId) > decimal.MaxValue - quantity
            || receiver.GetValue(itemId) > decimal.MaxValue - value)
            return false;

        return inventories.TryTransfer(itemId, sender, receiver, quantity, value, out _);
    }
}
