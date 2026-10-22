# Business operation and production

The Business mod owns business creation, readiness, employment, production, utility stocks, accounts, and lifecycle. The Market mod owns purchasing and automatic procurement. Both use Inventory for physical stock and Vault for money. Catalogs are loaded and validated during Business.OnLoad, so dependent mods can query types before systems start.

## Starting a business

Use IBusinessService from the service registry.

1. Create a living citizen with a BankAccountComponent. Create and commit an owned property with a permitted type and sufficient AreaM2.
2. Call CreateBusiness with a command buffer and commit its structural changes. It creates a separate business account and inventory, or preserves an existing premises inventory. Duplicate pending creation is rejected.
3. Transfer owner money using TryFundBusiness, or enable the Utilities startup policy for a finite one-time grant and paid procurement of initial stock/equipment. Manual scenarios can seed equipment through IInventoryService.TryAdd; supply totalValue for valued assets.
4. Configure an operating-cost recipient with TrySetOperatingCostRecipient. Insurance and maintenance are modeled as a bundled daily service charge; this does not itself repair property condition.
5. Hire workers with TryHire. WorkerProfileComponent.Skills holds role skill levels; absent roles have level zero. Vacancies are limited by the type's workforce counts. A worker may have one active job. Wages cannot be below the configured starting wage.
6. With Utilities enabled, connections and shortage imports follow world-supply.yml. Use IUtilityService.Connect to configure a business's connection rates. Without Utilities, use TryConnectGrid and explicitly bootstrap supply with TryDepositUtilities.
7. Call TryActivateBusiness. Readiness requires controlled premises, equipment, staffed roles, a cost recipient where needed, and cash covering outstanding liabilities plus the greater of MinimumCapital or one day of fixed costs plus one hour of contracted wages.
8. Workers arrive with TryArriveAtWorkplace, then call TryCheckIn during their agreed shifts. Hiring alone does not create attendance. TryCheckOut ends attendance; TryLeaveWorkplace also clears workplace presence. The runtime automatically checks workers out when the current working window ends.

Shifts default to the business's opening windows. Custom shift windows can be supplied to TryHire. Opening is inclusive and closing exclusive, including overnight windows. Duplicate or overlapping windows are merged when computing pay. Equal opening/closing endpoints mean a closed interval.

Business registers its CitizenMind facts, candidate providers, actions, and goals during OnLoad. Active citizen minds can find and accept qualified jobs, travel to work, and attend shifts. Business activation remains an owner/scenario operation; filling a roster does not silently activate the business.

## Employment and citizen decisions

IBusinessService exposes these queries and operations:

- GetVacancies returns funded vacancies with the employer, role, available positions, minimum skill, and hourly wage.
- GetAvailableJobs filters vacancies for a living, unemployed citizen with a registered payment account and sufficient skills. Jobs are ordered by wage, then stable employer/role order.
- GetEmployment returns the citizen's current employer and contract, or null.
- GetNextShift returns the remaining current working window or the next one within eight days. It intersects the contract's shifts with the business's actual opening hours, including overnight windows. Suspended businesses have no attendable shift.
- GetUpcomingShifts returns full, unclipped upcoming working windows (eight days by default), including overnight shifts. CitizenMind binds contract identity and exact start/end times so stale shifts fail safely.
- TryResign records attendance through the current game time and terminates employment. The old contract remains on the employer's roster for outstanding wage payments. Resignation and dismissal never forgive wages.

Job applications take five simulation minutes. Each action binds a specific employer and role; hiring rechecks qualifications, funding, and available positions at completion, so competing applications cannot overfill a vacancy. The planner prefers higher wages through the application's cost. Citizens hold at most one active job.

Workplace travel is an abstract journey because there is no map/pathfinding system yet. WorkerProfileComponent.CommuteMinutes configures its duration (default 15 minutes; nonpositive or nonfinite values use the default). Interrupted journeys do not mark arrival or earn wages. IsAtWorkplace records presence on the active contract, and check-in requires that presence. TryArriveAtWorkplace is also available for scenario/import integrations that already model travel.

EmploymentScheduleSystem publishes confirmed shifts through CitizenMind's independent `business.employment` schedule source, preserving manual schedules, daily rules and other mods. Priority is 40, and preparation begins one commute plus one simulation tick before the start. These commitments participate in ordinary plan forecasts; they are not need emergencies. Dismissal/suspension withdraws current and future shifts. Seven days of outcomes remain inspectable.

Travel and work are interruptible. Hunger (90), exhaustion (95) and thirst (100) emergencies can interrupt work, checking the citizen out while retaining pay for actual attendance. The citizen can return and resume the shift after recovery. At the end, the work handler clears workplace presence and requires the domain's LastCompletedShiftEnd to reach the bound occurrence's end. Partial attendance through the end can complete a shift; payroll always pays only attended minutes. A future shift has its own completion threshold, so today's attendance cannot satisfy tomorrow. Predictions and hiring create no real labor or wage income.

Citizens with active minds also resign from a suspended employer that owes them wages, then become eligible to find another job. Employers with unpaid wages while suspended do not advertise new jobs. Scenario code may call TryResign directly for other reasons.

## Runtime order

UtilitySystem runs at priority -7 for accounts, connections, startup procurement, and domestic consumption. BusinessSystem runs at -5, before normal citizen/action systems. BusinessRestockSystem runs at 10, and HomeUtilitySystem fills domestic buffers at 11 after local production. They observe the callback destination tick because the host commits Clock.Tick after callbacks finish.

Each business tick:

- Validate owner and premises.
- Accrue proportional daily operating costs, record actual shift attendance, and settle payroll and operating bills.
- Check current staffing, equipment, and operating electricity/water.
- Advance the active batch's calendar duration, apply eligible attended worker-minutes, and attempt completion.
- If open, start the first feasible recipe whose output target is below target.
- Procurement then buys missing stock and utilities and delivers eligible waste for disposal.

Minutes are game-calendar minutes, not real seconds. Batch time advances overnight but pauses during suspension. Labor is supplied only from actual recorded attendance and is capped at the recipe requirement. Cashier/retail roles supply customer service capacity, not production labor. Other roles supply production labor. Everyone is paid for attended shift time, including idle time.

Transient resource shortages are exposed as Issue. ProductionIssue separately reports recipe shortages/output blockage, so a full output store does not prevent selling stock to free space.

## Production and storage

A business runs at most one batch at a time. Starting a batch atomically reserves inputs and equipment. Reserved goods remain part of UsedCapacity and TotalValue, but cannot be purchased or consumed. Required equipment is returned at completion; inputs are transformed.

Recipe duration and labor are independent. The wheat recipe requires 129600 elapsed minutes and 480 labor-minutes. More workers supply more labor per minute without shortening the calendar duration.

Recipe utility inputs are consumed once when the batch starts. Operating utility use is additional and proportional to operating hours. The bread recipe uses bulk water from its utility buffer rather than bottled water inventory.

Completion requires both duration and labor. It replaces the reservation with returned equipment, products, and waste in one inventory exchange. If the complete output cannot fit, no output is committed, and the ready batch is retried later. Utility outputs are credited in the same completion operation.

TryCancelBatch returns reserved materials/equipment and discards time/labor progress. Utility consumption and wages already paid are not refunded. Reservation IDs prevent duplicate completion/release.

ProductionTargets maps product item IDs to stock targets; unspecified products default to two batches of output. Utility producers target two batches or their own configured buffer requirement, whichever is greater. Production resumes when sales/consumption deplete targets.

Storage capacity remains on inventories, not properties. It uses abstract quantity units. QuantityStep defaults to one whole item; material/waste catalogs use 0.001 increments. Use inventory services rather than modifying the public dictionary directly if conservation and valuation are required.

## Procurement, utilities, and waste

Stock policies compare available plus reserved quantities against ReorderBelow, then replenish toward the current stock target. Demand can raise that target and its reorder threshold. Recipe inputs without an explicit stock policy receive a two-batch target and a one-batch reorder threshold. Higher output targets proportionally raise input procurement targets. Procurement supports partial fills across suppliers, available cash after unpaid liabilities, and storage. It executes real inventory and bank transfers. Business buyers use wholesale channels; citizens use retail.

GetOffers compares sellers, current prices, quantities, channel, current availability, and opening windows. Its default result contains only immediately available offers. Passing includeUnavailable: true also exposes empty, closed, and service-limited listings so missing supply remains discoverable. These queries do not record demand or mutate inventory. Sellers must be operating and have customer service capacity where configured.

TryBuyOffer executes a particular quoted seller instead of silently choosing a replacement. It rechecks seller identity, item listing, channel, opening/operating state, stock, service capacity, buyer identity, storage, and spendable funds. The payment account and receiving inventory must belong to the same registered citizen or business. If the current price exceeds the quoted price, it returns PRICE_CHANGED; a lower current price is accepted. Stock is not reserved by a quote. TryBuy retains immediate cheapest-first exact purchasing; TryBuyUpTo combines partial orders across sellers. Liquidation sales use their separate explicit API and are excluded from normal demand signals.

The inventory transfer and payment are committed together, with ledger arithmetic checked first and bank balances restored if the inventory transfer fails. Successful purchases immediately update seller revenue, cost of goods sold, buyer acquisition value, and business stock purchases. Notifications do not apply those totals a second time.

## Citizen shopping and demand observation

Market registers a purchase action for each catalog item. Each candidate binds the specific seller EntityRef, quoted unit price, requested quantity, available quantity, and opening window. A purchase takes five game minutes, predicts the inventory increase, bank debit, and storage use, and commits only at successful completion. Cancelling it changes neither money nor stock. Quotes that become invalid cause failure and replanning. The planner checks money and storage across a sequence of purchases; eating predicts the storage that it frees.

Food purchase actions extend the existing hunger goal through ICitizenMindService.AddGoalActions during registration. Citizens can plan buy/eat sequences using the original consumption handlers and needs effects. Other modules can add the generic purchase actions to their own goals. Purchasing does not create goods; an actual stocked supplier is required.

CitizenDemandSystem observes active, living, hungry citizens who have no edible stock. It chooses one food intention, preferring affordable available food and comparing price per hunger relief. When no shop can supply it, it still reports that intention, using unavailable listings or catalog reference prices. This is separate from planning: an empty candidate list must not hide demand. Need resolution or a change of substitute clears the obsolete intention.

ReportDemand and failed purchases track one outstanding quantity per buyer inventory and item. Repeating the same unsatisfied order updates that quantity rather than adding it again every tick. Partial purchases reduce the remainder; fulfillment clears it. A later purchase after fulfillment represents fresh demand. Explicitly reported failures remain in the rolling history after resolution. Insufficient funds, insufficient storage, closed sellers, unavailable service, changed quotes, and stock shortages remain distinct statuses.

## Market measurements and adaptive policy

GetStatistics(itemId, optionalSeller) returns a rolling game-day view:

| Measurement | Meaning |
|---|---|
| SoldQuantity / Revenue | Completed normal sales and actual payments |
| PendingQuantity | Outstanding requests, including currently available goods awaiting purchase |
| UnmetQuantity | Outstanding requests currently blocked by a failure |
| ShortageQuantity | Outstanding requests specifically blocked by missing stock |
| DemandQuantity | Completed sales plus outstanding requests, without double-counting fulfilled shortages |
| Failures | Historical failed quantities by reason; repeated retries of an outstanding request are deduplicated |
| StockLevel / AverageStock | Current sellable inventory and the average of hourly stock samples |
| InventoryTurnover | Sold units divided by sampled average stock; null when the average is zero |

Measurements aggregate retail and wholesale trade. They are trade volumes, not a count of final consumption across a supply chain. Request observations expire after a day without renewal; sales, failures, and stock samples retain a rolling day. Removed sellers' historical sales remain in the global rolling totals until expiry.

MarketSystem runs after BusinessSystem at priority -4; citizen demand observation runs at -3, before CitizenMind. Policy changes occur at most once per game hour. A long time jump applies one bounded adjustment, rather than inventing missed hourly observations.

The adaptive signal includes sales, affordable pending orders, and stock shortages. Unaffordable interest is measured but does not raise the supply signal. Demand is smoothed with 75% of the previous estimate and 25% of the new estimate. Desired stock covers twice that measured daily demand, bounded between the configured baseline and four times the baseline. Each update changes the target by at most 25% of the baseline. Configured definitions are not rewritten.

Production uses these raised output targets, while procurement scales resale stock and recipe inputs. When demand subsides, targets move gradually back toward their configured baseline. Physical labor, funds, inputs, utilities, and storage still constrain production and procurement.

The base selling price is the greater of the configured floor and weighted inventory unit cost plus TargetMarkupPercent. The last sold unit cost is retained when stock runs out, preventing an empty shelf from erasing known cost. Stock pressure and effective demand set a scarcity multiplier between 1 and 2, moving by at most 0.05 per hour. Excess stock reduces the multiplier toward 1. Cost changes can still change the underlying base price; the movement limit applies to the scarcity multiplier.

Electricity and water use separate kWh/litre buffers. Connected businesses buy supply toward UtilityBufferHours (default 24) plus recipe batch needs. Producers must have actual stock and a matching GridSupplyPolicy. Export rates apply across all consumers in a tick: 30 kW allows 2.5 kWh per five-minute tick. Utility output is never treated as free stock or discarded on completion.

Water Works is a natural-source producer requiring a pump, electricity, staff, and time. Waste-to-energy uses organic waste and creates power plus ash. Utilities can bootstrap energy and other stock through paid imports; without that mod initial stocks remain a scenario responsibility. Adding business definitions does not automatically construct businesses.

Waste mass is declared with Item.MassKgPerUnit. Disposal converts kilograms to tonnes before applying fees, total capacity, and daily intake limits. Recovery facilities receive real inventory for processing. Permanent landfills bury accepted items and retain the quantity as DisposedTonnes against lifetime capacity, rather than filling a small working inventory. Organic-waste producer catalogs also expose waste wholesale offers for the energy facility to procure.

These are abstract deliveries/connections; routes, trucks, construction, and geographic utility networks belong to other simulation modules.

## Configurable city supply and world imports

The Utilities mod implements `IUtilityService`. Market discovers its smaller `ICitySupplyService` interface at runtime, so Market can still operate without world imports when Utilities is absent.

The checked-in configuration is `CitizenSimulator.Utilities/Configuration/world-supply.yml`. Building copies the default beside the mod DLL in `Mods/Core/Configuration`. To keep an installation-specific override across builds, place `Configuration/world-supply.yml` beside the host executable; this path takes precedence. Configuration is loaded on startup, so restart after changing it. Unknown fields, duplicate YAML keys, unknown items, mismatched units, and negative prices/capacities are rejected.

```yaml
utilities:
  electricity:
    enabled: true
    unit: kWh
    unitPrice: 2
    maxPerHour: 100000
  water:
    enabled: true
    unit: litre
    unitPrice: 0.02
    maxPerHour: 1000000

items:
  material.wood:
    enabled: true
    unit: kg
    unitPrice: 3
    maxPerDay: 100000
```

Omit an entry or set `enabled: false` to prohibit that world purchase. Limits are shared across the entire city. Item limits reset each game day. Utility rate limits apply per game tick: 120 kWh/hour permits 10 kWh in a five-minute tick. Connection rates separately limit each receiver, and each local supplier's export rate is shared across its customers. Repeated calls in one tick cannot reset these budgets.

### Local supply before world supply

`ReplenishStock(businessEntity, itemId, quantity)` first buys from actual local wholesale offers using Market. Local opening hours, stock, access, service capacity, funds, and storage apply. It imports only the remaining quantity allowed by the whitelist, daily quota, funds, and receiving storage. Item quantities round down to QuantityStep. Imported stock receives its actual acquisition cost, and the outstanding market request is reduced by the imported quantity without inventing a domestic sale.

`SupplyUtility(businessEntity, utility, targetQuantity)` fills toward a total buffer target, rather than adding that quantity every call. It buys available local supply first, then world supply for the remainder. For example, a 500 kWh target supplied with 300 exportable local kWh imports exactly 200 kWh, provided connection, export, world-import capacity, and funds allow it. Local suppliers retain their own operating and batch reserve. Local supply takes priority even when its configured price is higher than the world price.

`Connect` configures a business's electricity capacity in kW and water capacity in litres/hour. `Disconnect` stops further network deliveries, and automatic connection does not undo that explicit choice. Existing paid business buffers remain usable; they represent the current abstract energy/water reserve rather than a geographic cable or pipe model. Imports stop when funds or limits run out; normal business utility checks then expose MissingUtilities and prevent unsupported operation or production.

Import payments go into an actual registered `WorldAccount`. They are external payments, not income for a fictitious local seller. Businesses record `StockPurchases` or `UtilityPurchases`; utility consumption remains an expense through `UtilityCostsConsumed`.

### Startup stock and working capital

With `startup.enabled: true`, setting-up businesses receive at most one grant from a finite `StartupFund`. `StartupFundingAwarded` records that the grant decision has happened, including when existing funds made a zero grant sufficient. Draining the account cannot trigger another award. Exhausting the fund leaves unfunded businesses in setup.

The budget covers required equipment, explicit stock targets, all recipe inputs, initial utility buffers, and a working-capital reserve. The reserve uses 24 hours of desired payroll and operating utilities per configured day, plus fixed daily costs; this is deliberately conservative. The default is seven days; the wheat farm overrides it to 95 days to cover its 90-day first harvest. Imports of wheat, flour, and bread can bridge the city's food supply while local farms mature. Fruit, milk, water portions, timber, iron, and required equipment also have configurable import sources.

The grant is a separately recorded capital inflow, not sales revenue or owner funding. Stock and equipment are then purchased through the normal local-first delivery path. The layer does not create buildings, hire workers, invent attendance, or activate businesses. Those remain owner/scenario/citizen operations. Disable startup grants for scenarios that supply their own capital.

### Waste collection and recovery

`CollectWaste` uses explicit abstract delivery: accepted waste arrives immediately at the selected facility, and the facility fee includes collection and delivery. `collectionTonnesPerDay` bounds city-wide transport capacity; routes, trucks, and travel times are not simulated.

Collection tries recovery facilities first, then permanent landfills, then an enabled world disposal route for any remaining waste. Each local facility enforces operating state, accepted items, daily intake, physical recovery storage, and lifetime landfill capacity. Collection only removes accepted, paid quantities; uncollected waste stays in the source inventory and can block production storage.

The incinerator accepts organic waste into inventory rather than burying it. Its real recipe consumes that stock, requires equipment, labor, time and utilities, and creates 30 kWh plus ash per batch. That generated buffer participates in local electricity sales. Ash then requires separate collection and disposal. Recovery capacity becomes available again after processing; permanent burial capacity never resets. World disposal also has daily and lifetime limits plus a configurable fee per tonne.

### Domestic utility meters

Homes participate in the same local-first utility supply and shared world-import limits. Household policy configures connection rates, buffer hours, and background consumption per resident per day. The living property owner pays the shared meter, so the current policy treats utilities as included in rent. No automatic household grant is supplied.

`ConnectHome`, `DisconnectHome`, and `GetHomeConnection` expose the meter. It records delivered buffers, purchases, actual consumption, and unserved background demand. When Utilities is loaded, safe-water access requires actual water, and cooking access requires both water and electricity. The home-water drinking handler consumes an additional quarter litre per active drinking minute and rechecks availability before reducing thirst. Sleeping still requires a bed and usable residence, not electricity. Empty buffers or unpaid replenishment do not grant water access.

### Quantity units and measurements

| Resource | Quantity unit |
|---|---|
| Food and packaged drinks | One consumable portion; consumption removes one portion |
| Packaged drinks | LitresPerUnit declares volume: water/milk/juice 1 litre, coffee 0.25 litre |
| Wheat, flour, timber, iron, stone | Kilograms; QuantityStep permits 0.001 kg |
| Organic waste and ash | Kilograms; fees/capacities convert using MassKgPerUnit / 1000 to tonnes |
| Equipment and furniture | Whole pieces |
| Network electricity | kWh; connection/export power is kW, multiplied by elapsed game hours |
| Network water | Litres; connection/export rates are litres/hour |
| Physical inventory storage | Abstract storage units: one base item quantity occupies one unit |

Storage units are a gameplay capacity measure, not kilograms, litres, or cubic metres. Utility buffers are separate from physical inventory storage. Bottled drinking-water portions and network-water litres are separate resources; the bakery's water is already declared in UtilitiesPerBatch and operating utilities, so procurement does not substitute bottles for it.

`GetStatistics(resourceId)` exposes cumulative local deliveries, imports, import costs, collected/disposed tonnes, disposal fees, and current outstanding delivery shortages. Requested counts delivery attempts, including retries; it is not a production-consumption forecast. Item and utility shortages use their native quantity units, while waste collection/disposal counters use tonnes. Business utility consumption and domestic unserved demand are available on their respective business/meter records.

Run the supply integration checks with:

```powershell
dotnet build tests/CitizenSimulator.UtilityChecks --configuration Release --disable-build-servers -m:1 -p:UseSharedCompilation=false
dotnet run --project tests/CitizenSimulator.UtilityChecks --configuration Release --no-build --no-restore
```

## Accounting

Every physical inventory item carries a total acquisition value. Removal uses weighted-average cost. Reservations retain value. Material input value is allocated across output units; waste has zero allocated value. Equipment retains its own value. Labor and utility consumption are period expenses rather than capitalized conversion costs. For utility-only production, material input value is expensed at completion.

BusinessComponent exposes:

- SalesRevenue, StockPurchases, CostOfGoodsSold, and ProductionLosses.
- OperatingCostsAccrued, OperatingCostsPaid, and UnpaidOperatingCosts.
- WagesAccrued, WagesPaid, and UnpaidWages.
- UtilityPurchases, UtilityCostsConsumed, and DisposalCosts.
- OwnerFunding, StartupFunding, and OwnerWithdrawals.
- Profit = sales revenue minus recognized expenses.
- OperatingCashFlow = sales receipts minus stock purchases, operating payments, wage payments, utility purchases, and disposal fees.
- NetCashFlow = operating cash flow plus owner funding and startup funding minus withdrawals.

Funding is never sales revenue. Buying stock creates an asset, not immediate cost of goods sold. Revenue/expense totals update immediately on confirmed transactions. OnBusinessSale is an observation event and must not be used to credit these totals a second time.

Inventory seeded without totalValue has zero initial basis. This is deliberate scenario setup, not an estimate of market value. Test fixture prices/wages demonstrate mechanics; the catalog economy still needs scenario-specific balancing.

## Suspension, closure, and bankruptcy

Failed payroll or operating payment retains debt, stops activity, and suspends the business. Recorded wages are not forgiven. Financial suspension starts an insolvency timer. A business that remains unable to cover its liabilities becomes bankrupt after BankruptcyGraceDays (default seven); explicit TryDeclareBankrupt is also available.

TryFundBusiness can fund debt settlement even after closure/bankruptcy. TryPayWages and TryPayOperatingCosts settle outstanding claims. Resume uses TryActivateBusiness and rechecks readiness. Workers must check in again; suspended time never turns into retroactive labor.

TryCloseBusiness cancels its batch before entering ShutDown. Bankrupt and shut-down businesses never automatically reactivate.

TryBuyLiquidationStock explicitly sells terminal-business assets at an agreed price, without normal retail hours. Grid clients can also buy remaining supplier utility stocks. TryLiquidateBusiness requires all liabilities settled, utility stocks cleared, and a living owner with receiving inventory/account capacity. It returns remaining assets/cash to the owner, unregisters the empty business account, and queues removal of the business component, freeing the premises. Existing bankruptcy creditors are never silently written off.

## Checks

From the repository root:

~~~powershell
dotnet restore tests/CitizenSimulator.BusinessChecks --disable-parallel -m:1 -p:NuGetAudit=false
dotnet build tests/CitizenSimulator.BusinessChecks --no-restore -m:1
dotnet run --project tests/CitizenSimulator.BusinessChecks --no-build --no-restore
~~~

The integration executable uses real ECS command playback, business ticks, market transactions, game-calendar time, and CitizenMind planning/execution. It covers conservation, readiness, attendance, payroll, blocked outputs, crop timing, utilities, procurement, lifecycle, job acceptance and resignation, overnight shifts, emergencies, seller-bound quotes, access checks, deduplicated shortages, partial fulfillment, demand-driven production and replenishment, bounded prices, and autonomous buy/eat plans with limited funds and storage.
