# Entities and components

## EntityRef

`EntityRef(int Id, int Generation, int StoreId)` is a value identifying an entity within a store. Equality includes all three fields. Do not construct references from a guessed ID. The current implementation issues increasing IDs with generation `1` and assigns each store its own identifier.

The default value is a detached or absent reference. A reserved entity returned by a command buffer is also not alive until its create command plays. Use `Store.IsAlive(entity)` before reading a reference that might have been destroyed.

## Component contracts

`IComponent` exposes `Parent`. Its setter is internal to the contract. `Component<T>` implements this contract and gives derived components a publicly readable parent. Prefer deriving from `Component<YourComponent>` rather than implementing parent bookkeeping yourself.

The engine assigns `Parent` during direct attachment or when attachment is queued. A queued parent therefore does not prove that the component is already in the store. Removal, entity destruction, or discarded unattached commands clear the parent to its default value.

A component instance cannot belong to different entities simultaneously. Use a fresh instance for each entity. Components are stored by the exact generic type supplied to `AddComponent<T>`, so use the concrete component type consistently.

## IEntityStore calls

### CreateEntity()

Returns a new active `EntityRef` immediately. Use during mod loading or other host controlled setup outside scheduled callbacks. During callbacks call `commands.CreateEntity()` instead.

### DestroyEntity(EntityRef entity)

Immediately removes a live entity and clears all attached component parents. Invalid or destroyed references throw. Use the command buffer during callbacks.

### IsAlive(EntityRef entity)

Returns whether the reference belongs to this store, has the expected generation, and currently identifies an active entity. Returns false for pending reservations, destroyed references, and foreign references.

### AddComponent<T>(EntityRef entity, T component)

Immediately attaches and returns the same component instance. `T` must be a class implementing `IComponent`. The entity must be alive, the instance must be nonnull, and the entity must not already contain this exact component type. Attaching a duplicate throws rather than replacing data. During callbacks use the command buffer.

### GetComponent<T>(EntityRef entity)

Returns the component or null when that type is absent on a live entity. An invalid entity throws rather than returning null. Reading returns the stored reference, so changing its properties changes shared state immediately.

### HasComponent<T>(EntityRef entity)

Reports whether a live entity contains the exact component type. An invalid entity throws. This call cannot see attachments waiting in a command buffer.

### RemoveComponent<T>(EntityRef entity)

Immediately removes the type when present and clears the detached component's parent. Missing components are harmless on a valid entity. Invalid entities throw. Use the command buffer during callbacks.

### Query(IQuery query)

Lazily enumerates active entities whose component dictionaries match the query. A null query throws. Avoid direct structural mutation while enumerating. Queue structural work for the end of the callback instead.

## ICommandBuffer calls

Every method operates on the current callback's buffer. Never retain the buffer for another callback, another thread, or later asynchronous work. Playback and discard close it permanently.

### CreateEntity()

Reserves and returns an `EntityRef`, then queues activation. You may queue component attachments to this reference in the same buffer. Store reads still reject it until playback activates it.

### DestroyEntity(EntityRef entity)

Queues strict destruction. The reference must belong to this store and must not be reserved by another buffer. Liveness is enforced when the command executes. Destroying an already destroyed entity causes playback failure.

### TryDestroyEntity(EntityRef entity)

Queues destruction only if the entity is alive when playback reaches the command. Useful when multiple operations might destroy the same entity. Foreign references and reservations owned by another buffer still throw when queued. This is not a universal validation bypass.

### AddComponent<T>(EntityRef entity, T component)

Queues attachment and immediately returns the supplied object. `Parent` is assigned when queued, so you can configure the returned component before attachment. Duplicate type checks happen during playback. Repeated additions of the same type can therefore fail after earlier commands have already succeeded.

### RemoveComponent<T>(EntityRef entity)

Queues removal. Missing types are harmless at playback if the entity is alive. Removing from an entity destroyed by an earlier command fails validation.

## Creating related state

This is pseudocode for a callback that creates an entity and attaches data.

```text
entity = commands.CreateEntity()
counter = commands.AddComponent(entity, new CounterComponent { Count = 0 })
counter.Count = 5
```

Do not call `Store.GetComponent<CounterComponent>(entity)` in this sequence. The reference is still reserved. Retain the returned component object for work within this callback and read through the store in later callbacks.

## Component registry calls

`IComponentRegistry.Register<T>()` registers the type and returns `ComponentType<T>`. Registering the same type again returns its existing token.

`IComponentRegistry.Get<T>()` returns an existing token and throws when the type is unregistered. `IComponentRegistry.IsRegistered<T>()` checks registration without throwing for absence.

`ComponentTypes.Initialize(IComponentRegistry registry)` sets the global registry used by static helpers. The host calls it before loading mods. Mods should use the supplied registry rather than reinitialize this shared state.

`ComponentTypes.Get<T>()` forwards to that registry and throws if initialization has not occurred or the type is absent. `Component<T>.GetComponentType()` is the convenient inherited form.

`ComponentType<T>(int Id)` implements `IQuery`. Its `Matches` method checks the presence of `typeof(T)` in a component dictionary. The numeric ID is registry metadata rather than a persistent identifier to save or transmit.
## Housing and property transactions

The Housing mod registers `IHousingService` and `IHomeAccessService`. Its system runs at priority -8, before business and citizen decisions. A `Residence` links a living citizen to one `HouseComponent`; the house exposes a read-only occupant roster. Use the service to change occupancy so both directions stay consistent.

### Creating and occupying a home

1. Create and commit a residential or mixed-use property through `IPropertyService`. Its `Capacity` limits occupants, and its owner must be a living citizen.
2. Call `IHousingService.CreateHousing` with a command buffer and commit it. Choose occupant capacity, storage units, beds, safe water, and kitchen access. Duplicate creation and capacity beyond the property's limit are rejected. Storage is housing-specific and does not add a storage field to `PropertyComponent`.
3. Owners can call `TryMoveIn` directly. To offer spare places, call `ListRental(owner, house, dailyRent, arrearsGrace)`. Rental prices are per resident per game day; grace must be at least one day. Listing or relisting changes future offers, not existing leases.
4. `FindHomes` returns eligible homes, preferring the citizen's own property, then cheaper rentals. `TryMoveIn` binds the rental listing's ID and rechecks ownership, capacity, funds, and outstanding rent debt. A failed destination check preserves the current home.
5. `GetResidence` returns the current home and lease. `TryMoveOut` closes the lease and removes the occupant. Money owed and belongings remain recorded.

Each tick automatically allocates homeless citizens to eligible homes. Displaced residents are considered first. Renting requires a registered citizen bank account, cash covering one day of rent, and no outstanding rent debt to a living landlord. Estate claims remain recorded without blocking relocation. Existing tenants are not displaced to make room for an owner. If nothing is available or affordable, the citizen remains homeless and allocation retries next tick. Scenario mods must create actual properties and homes; this mod does not generate buildings or money.

### Rent, sales, and repairs

Rent accrues in game time, prorated from the lease's start. Cumulative calculation avoids drift from splitting a day into small ticks. Available tenant funds are transferred to the landlord through Vault; partial payments leave arrears. A continuous unpaid period beyond the agreed grace ends occupancy. `GetLeases` includes closed contracts and their debts; `TryCollectRent` can settle old arrears. Closing a lease stops future rent, without forgiving debt.

`ListSale` exposes a property with a seller, listing ID, and price. `TryBuyProperty(buyer, listingId, expectedPrice)` rechecks the offer, current owner, registered accounts, funds, and receiver overflow. Payment precedes ownership transfer; a rejected ownership transfer reverses payment. Listings execute once. A successful sale removes the old owner's rental offer, retains tenants at their agreed rent, closes the old creditor's lease ledger, and starts future rent for the buyer. Pre-sale debt stays payable to the original landlord. An occupying seller moves out unless they already have a tenant contract; selling occupied premises does not grant a buyer another resident's goods.

`TryRepair(owner, property, contractor, conditionPoints, expectedCost)` pays a living contractor and restores condition together. The current policy costs 10 currency units per condition point, available as `HousingService.RepairCostPerPoint`. Invalid quotes, insufficient funds, overflow, over-repair, and destroyed properties are rejected. Periodic property deterioration still applies. Business operating-cost maintenance charges do not automatically call this repair service.

### Access, storage, and cleanup

A property is habitable at condition 20 or above and must not be destroyed. `CanSleep`, `CanDrink`, `CanCook`, and `CanStore` require a living resident and owner and a usable home. Sleeping additionally requires beds; drinking requires safe water; cooking requires a kitchen and safe water. With Utilities loaded, drinking also requires an actual water buffer, and cooking requires water and electricity. The utility layer bills the owner and consumes domestic water/energy; see [city supply](business.md#configurable-city-supply-and-world-imports). Cooking recipes remain a separate feature; their handlers can use `IHomeAccessService.CanCook` to enforce kitchen access.

Needs registers `home.can_sleep`, `home.can_drink`, `home.can_cook`, and `home.can_store` through that interface. Sleep and home-water handlers recheck access at start and every tick. Resting outdoors remains possible. Existing inventory consumption and needs modification are unchanged.

`TryStore` transfers actual carried inventory into resident-owned storage at the current home. All residents' stored goods share the home's storage limit. `TryRetrieve` returns only that citizen's goods and enforces carried inventory capacity. Item quantities and book value are preserved through Inventory. After move-out or eviction, belongings stay at the original location and can still be recovered; they do not move automatically to the new home. Retrieval from a destroyed home currently represents abstract salvage, without a travel or salvage-time simulation.

Unusable or removed premises end occupancy and trigger relocation. Dead occupants are removed and their leases close. A dead owner's listings disappear, ownership becomes unassigned, and residents seek other homes. Old rent debts and stored belongings remain attached to their original parties for estate handling; inheritance and automatic distribution of estates are not implemented.

Run the integration checks with:

```powershell
dotnet run --project tests/CitizenSimulator.HousingChecks --configuration Release --disable-build-servers -p:UseSharedCompilation=false
```
