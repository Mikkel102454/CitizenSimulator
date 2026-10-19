# Game services

These APIs belong to optional mod assemblies rather than the core abstraction project. Add a project or assembly reference for the contract types, declare the provider's mod ID as a dependency, then call `context.Services.Get<T>()` with the registered interface.

The names `Calender` and `Abstraction` below preserve the spelling used by the source code. A boolean return describes service acceptance, but queued structural work can still fail during later command playback.

## Citizens

Provider ID `citizen`. Interface `CitizenSimulator.Citizen.Services.Abstraction.ICitizenService`.

`CreateCitizen(EntityRef entityRef, string name, int age, out CitizenComponent? outCitizen)` attaches a citizen to an existing live entity, sets its name and age, marks it alive, and queues `CitizenCreated`. Returns false with a null output when the component already exists.

This method directly calls `Store.AddComponent`. It therefore cannot be called successfully from a scheduled callback under the structural mutation guard. Use it during loading, or implement a command based creation path in your mod with the appropriate event publication.

`KillCitizen(CitizenComponent citizen)` sets `Alive` to false and queues `CitizenDied`. It does not destroy the entity, and repeated calls can publish repeated death events.

## Needs

Provider ID `needs`. Interface `CitizenSimulator.Needs.Services.Abstractions.INeedsService`.

`SetHunger(NeedsComponent needsComponent, float hunger)`, `SetThirst(NeedsComponent needsComponent, float thirst)`, and `SetEnergy(NeedsComponent needsComponent, float energy)` set the named value after clamping it to the inclusive range from `0` to `100`. Nonfinite values return false.

`AddHunger(NeedsComponent needsComponent, float hunger)`, `AddThirst(NeedsComponent needsComponent, float thirst)`, and `AddEnergy(NeedsComponent needsComponent, float energy)` add to the current value through the corresponding setter. Negative finite changes are allowed. Overflow to a nonfinite result is rejected.

These calls mutate existing component data immediately and do not attach missing needs components.

## Items

Provider ID `items`. Interfaces are in `CitizenSimulator.Items.Services.Abstractions`.

`IItemService.GetItems()` returns an array snapshot of the current item catalog. `GetConsumableItems()` returns a filtered array containing items whose `Consumable` flag is true. The arrays are new, but their item objects are shared references.

`IConsumptionService.ConsumeItem(Item item, CitizenComponent citizen)` rejects dead citizens, nonconsumable items, and citizens without needs data. It processes recognized `needs.modify` effects for hunger, thirst, and energy and returns true afterward. It does not remove inventory stock or pay for an item. Unknown effect types are ignored. Malformed effect value dictionaries can throw.

## Wallets

Provider ID `vault`. Interface `CitizenSimulator.Vault.Services.Abstractions.IWalletService`.

`CreateWallet(ICommandBuffer commands, EntityRef entityRef, out WalletComponent? outWallet)` checks for an existing wallet, then queues attachment and returns the pending component. The existence check reads the store, so the entity must already be alive. Repeating this call before playback can queue duplicate attachments.

`DeleteWallet(ICommandBuffer commands, WalletComponent wallet)` queues removal using `wallet.Parent`.

`AddBalance(WalletComponent wallet, decimal amount)` adds a nonnegative amount unless the result would overflow. `RemoveBalance(WalletComponent wallet, decimal amount)` requires a nonnegative amount and sufficient funds. `SetBalance(WalletComponent wallet, decimal amount)` accepts nonnegative balances. Failed validation returns false without applying the balance change.

## Vault accounts

Provider ID `vault`. Interface `CitizenSimulator.Vault.Services.Abstractions.IVaultService`.

`CreateVault(ICommandBuffer commands, EntityRef entityRef, string name, out VaultComponent? outVault)` checks a live entity for an existing vault, then queues a named vault attachment. `DeleteVault(ICommandBuffer commands, VaultComponent vault)` queues removal.

`AddBankAccount(VaultComponent vault, out VaultComponent.Account outAccount)` creates an account with zero balance and immediately adds it to the vault's account collection. `RemoveBankAccount(VaultComponent vault, VaultComponent.Account account)` attempts collection removal and currently returns true even if the account was absent.

`AddBalance(VaultComponent.Account account, decimal amount)` and `RemoveBalance(VaultComponent.Account account, decimal amount)` enforce nonnegative amounts, capacity, and sufficient funds. `SetBalance(VaultComponent.Account account, decimal amount)` rejects negative balances.

`TransferBalance(VaultComponent.Account senderAccount, VaultComponent.Account receiverAccount, decimal amount)` validates the amount, sender funds, and receiver capacity before updating both accounts. A transfer to the same account returns true after validation without changing its balance. Account operations do not independently verify membership in a particular vault.

## Properties

Provider ID `property`. Interface `CitizenSimulator.Property.Services.Abstractions.IPropertyService`.

`CreateProperty(ICommandBuffer commands, EntityRef entityRef, PropertyComponent.PropertyType type, int capacity, double baseValue, CitizenComponent? owner, out PropertyComponent? outProperty)` checks a live entity for an existing property, queues attachment, fills the supplied fields, and publishes `OnPropertyCreated`. It returns false for an existing property. Capacity and base value are assigned without further validation in this service.

`TransferOwnership(PropertyComponent property, CitizenComponent? newOwner)` immediately changes the owner and publishes `OnPropertyOwnershipChanged`. Null ownership is accepted.

`DestroyProperty(PropertyComponent property)` marks the property destroyed and publishes `OnPropertyDestroyed`. It does not destroy the entity or remove the property component.

## Businesses

Provider ID `business`. Interface `CitizenSimulator.Business.Services.Abstractions.IBusinessService`.

`CreateBusiness(ICommandBuffer commands, string typeId, string name, CitizenComponent owner, PropertyComponent property, out BusinessComponent? outBusiness)` is intended to create business data, inventory, and a payment account on premises with an available vault.

The current implementation returns false when the entity already has a `PropertyComponent`, which includes the normal case of attached premises. It also does not use `typeId` to assign a business type. Treat this as an implementation limitation. A successful boolean alone would not guarantee a fully configured operating business.

## Market

Provider ID `market`. Interface `CitizenSimulator.Market.Services.Abstractions.IMarketService`.

`TryBuy(InventoryComponent receiveInventory, VaultComponent.Account paymentAccount, string itemId, int amount)` enumerates business entities and attempts a purchase from the first seller that passes validation. It does not choose the cheapest seller.

`TryBuyFrom(BusinessComponent seller, InventoryComponent receiveInventory, VaultComponent.Account paymentAccount, string itemId, int amount, decimal maximumUnitPrice)` attempts a purchase from a specific seller. The amount must be positive and the price cap nonnegative. The seller must be an attached operating business with inventory, a payment account, and a sales policy listing the item. Stock, balances, and arithmetic capacity must permit the purchase. Buyer and seller inventory or account objects cannot be the same.

On success payment transfers and both inventories update immediately. Validation failure returns false. Supply valid nonnull component and account objects because these methods do not validate every possible malformed caller object.

## Calendar

Provider ID `calender`. Interface `CitizenSimulator.Calender.Services.Abstractions.ICalenderService`.

`GetTicksPerDay()` currently returns `300`. This is ten simulation seconds only at a tick rate of `30`.

`GetTimeOfDay()` returns an hour and minute string derived from the calendar's tick of day. It does not pad digits. The concrete service also has `GetTickOfDay()`, but that member is absent from `ICalenderService` and is not part of the registered interface contract.

Calendar events include `OnDayStart`, `OnWeekStart`, `OnMonthStart`, `OnYearStart`, `OnMorningStart`, and `OnNightStart`, defined under `CitizenSimulator.Calender/Events`.

## Brain extension status

The mod ID `citizen_brain` currently has an empty `OnLoad`. Its presence alone does not register `BrainService`, a catalog, a runtime registry, or the brain system. Do not assume `Services.Get<BrainService>()` will succeed after declaring the dependency.

The supporting classes provide extension building blocks if your mod deliberately constructs and wires them.

`BrainCatalog.Load()` reads embedded definitions from the brain assembly. It does not discover arbitrary JSON beside your DLL. `BrainCatalog.Parse(Stream stream)` parses and validates one definition. Constructing `BrainCatalog(IEnumerable<DecisionDefinition> definitions)` validates unique IDs and requires the `needs.idle` fallback. `Definitions` exposes the validated collection.

`BrainRuntimeRegistry.RegisterSource(name, provider)` adds a candidate provider. `RegisterFact(name, provider)` adds a fact provider. `RegisterHandler(name, handler)` adds an action handler. Each registration increments `Revision`. Names are case sensitive and duplicate registration throws.

`HasSource(name)` checks source availability. `TryGetHandler(name, out handler)` attempts handler lookup. `Candidates(name, context)` returns provider candidates or an empty collection. `Fact(name, context, candidate)` prefers candidate facts, then a registered provider, then null.

`BrainService.Catalog` and `Runtime` expose the injected collaborators. `Inspect(definition, context)` evaluates candidate availability and scores. `Update(context, commands)` updates planning and running actions. These are gameplay implementation APIs rather than core abstraction contracts. Read `CitizenSimulator.CitizenBrain/Models` and `Services` when constructing a custom integration.
