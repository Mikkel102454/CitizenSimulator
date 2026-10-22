using CitizenSimulator.Abstractions.Events;
using CitizenSimulator.Business.Components;

namespace CitizenSimulator.Business.Events;

public sealed record OnBusinessSale(BusinessComponent Business,
    string ItemId,
    decimal Quantity,
    decimal TotalPrice,
    BusinessComponent? Buyer = null) : IEvent;
