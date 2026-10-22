# Business operation and production

The Business mod owns business creation, readiness, employment, production, utility stocks, accounts, and lifecycle. The Market mod owns purchasing and automatic procurement. Both use Inventory for physical stock and Vault for money. Catalogs are loaded and validated during Business.OnLoad, so dependent mods can query types before systems start.

## Starting a business

Use IBusinessService from the service registry.

1. Create a living citizen with a BankAccountComponent. Create and commit an owned property with a permitted type and sufficient AreaM2.
2. Call CreateBusiness with a command buffer and commit its structural changes. It creates a separate business account and inventory, or preserves an existing premises inventory. Duplicate pending creation is rejected.
3. Transfer owner money using TryFundBusiness. Buy or explicitly seed equipment through IInventoryService.TryAdd. Supply totalValue when seeding valued assets.
4. Configure an operating-cost recipient with TrySetOperatingCostRecipient. Insurance and maintenance are modeled as a bundled daily service charge; this does not itself repair property condition.
5. Hire workers with TryHire. WorkerProfileComponent.Skills holds role skill levels; absent roles have level zero. Vacancies are limited by the type's workforce counts. A worker may have one active job. Wages cannot be below the configured starting wage.
6. Connect utility access using TryConnectGrid. Existing suppliers can fill utility buffers through procurement. A new scenario must explicitly provide initial utility stocks/imports with TryDepositUtilities to bootstrap the power/water supply chain.
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
- TryResign records attendance through the current game time and terminates employment. The old contract remains on the employer's roster for outstanding wage payments. Resignation and dismissal never forgive wages.

Job applications take five simulation minutes. Each action binds a specific employer and role; hiring rechecks qualifications, funding, and available positions at completion, so competing applications cannot overfill a vacancy. The planner prefers higher wages through the application's cost. Citizens hold at most one active job.

Workplace travel is an abstract journey because there is no map/pathfinding system yet. WorkerProfileComponent.CommuteMinutes configures its duration (default 15 minutes; nonpositive or nonfinite values use the default). Interrupted journeys do not mark arrival or earn wages. IsAtWorkplace records presence on the active contract, and check-in requires that presence. TryArriveAtWorkplace is also available for scenario/import integrations that already model travel.

A shift becomes due one commute plus one simulation tick before its start. Its goal uses interrupt priority 40, below the existing hunger (90), exhaustion (95), and thirst (100) emergencies. Travel and work are interruptible. An emergency checks the citizen out immediately, retaining pay for completed attendance; the citizen can travel back and resume the remaining shift after recovery. A work action occupies the citizen until the working window ends, then checks out and clears workplace presence. Neither planning predictions nor hiring directly create labor or wage income.

Citizens with active minds also resign from a suspended employer that owes them wages, then become eligible to find another job. Employers with unpaid wages while suspended do not advertise new jobs. Scenario code may call TryResign directly for other reasons.

## Runtime order

BusinessSystem runs at priority -5, before normal citizen/action systems, and BusinessRestockSystem at priority ten. It observes the callback destination tick because the host commits Clock.Tick after callbacks finish.

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

Stock policies compare available plus reserved quantities against ReorderBelow, then replenish toward TargetQuantity. Procurement supports partial fills across suppliers, available cash, and storage. Offers are ordered by price and stable seller entity ID. Business buyers use wholesale channels; citizens use retail. Sellers must be operating and have customer service capacity where configured.

Configured selling prices are floors. Known stock costs plus TargetMarkupPercent can raise the price above that floor. This is cost-based pricing, not a demand-auction model.

Electricity and water use separate kWh/litre buffers. Connected businesses buy supply toward UtilityBufferHours (default 24) plus recipe batch needs. Producers must have actual stock and a matching GridSupplyPolicy. Export rates apply across all consumers in a tick: 30 kW allows 2.5 kWh per five-minute tick. Utility output is never treated as free stock or discarded on completion.

Water Works is a natural-source producer requiring a pump, electricity, staff, and time. Waste-to-energy uses organic waste and creates power plus ash. Initial energy/import stock is explicitly scenario supplied. Adding business definitions does not automatically construct these businesses.

Waste mass is declared with Item.MassKgPerUnit. Disposal converts kilograms to tonnes before applying landfill fees, total capacity, and daily intake limits. Physical waste is transferred to the disposal business's inventory; it does not disappear. Organic-waste producer catalogs expose waste wholesale offers for the energy facility to procure.

These are abstract deliveries/connections; routes, trucks, construction, and geographic utility networks belong to other simulation modules.

## Accounting

Every physical inventory item carries a total acquisition value. Removal uses weighted-average cost. Reservations retain value. Material input value is allocated across output units; waste has zero allocated value. Equipment retains its own value. Labor and utility consumption are period expenses rather than capitalized conversion costs. For utility-only production, material input value is expensed at completion.

BusinessComponent exposes:

- SalesRevenue, StockPurchases, CostOfGoodsSold, and ProductionLosses.
- OperatingCostsAccrued, OperatingCostsPaid, and UnpaidOperatingCosts.
- WagesAccrued, WagesPaid, and UnpaidWages.
- UtilityPurchases, UtilityCostsConsumed, and DisposalCosts.
- OwnerFunding and OwnerWithdrawals.
- Profit = sales revenue minus recognized expenses.
- OperatingCashFlow = sales receipts minus stock purchases, operating payments, wage payments, utility purchases, and disposal fees.
- NetCashFlow = operating cash flow plus owner funding minus withdrawals.

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

The integration executable uses real ECS command playback, business ticks, market transactions, game-calendar time, and CitizenMind planning/execution. It covers conservation, readiness, attendance, payroll, blocked outputs, repeated completion/cancellation, crop timing, utilities, disposal units, procurement, lifecycle transitions, competing applications, resignation and arrears, overnight shifts, autonomous job acceptance and travel, and emergency interruption/resumption.
