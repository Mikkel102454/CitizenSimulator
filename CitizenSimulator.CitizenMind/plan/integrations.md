# World integration contracts

Names below marked **proposed** are design contracts, not existing C# interfaces. Keep a narrow brain-facing capability registry and adapters so domain mods can change without rewriting behavior definitions. Prefer separate bridge mods or optional capability registration over adding every world project as a direct CitizenBrain dependency.

## Repository baseline

| Domain | Present today | Missing for this design |
| --- | --- | --- |
| Brain | `CitizenBrain.cs` has an empty `OnLoad`; dependencies list needs, vault, calender, citizen | Configuration loading, state, decision systems, adapters |
| Engine | `IModContext` exposes services, ECS registries, events and clock; `GameSystem` supports ordered tick updates | A declared brain/world update boundary and versioned snapshots |
| Citizen | `CitizenComponent` has name, age, alive, birthday; creation/death events exist | Brain initialization/lifecycle wiring, skills and formal relationships |
| Needs | Hunger, Energy, Thirst fields; `INeedsService` mutation methods exist | Mutation methods currently throw `NotImplementedException`; consistent scales and outcome semantics |
| Calendar | `ICalenderService.GetTicksPerDay()` and string `GetTimeOfDay()`; date component and calendar events | Stable absolute game timestamp, typed local time/date, conversion and cross-midnight intervals |
| Business | `WorkforceRequirement` defines desired count, role, minimum skill and starting hourly wage; `OpeningHours` defines opening windows | Staff roster, funded vacancies, applications, offers, employment contracts, employee shifts, attendance and payroll |
| Market | `IMarketService.TryBuy(receiveInventory, paymentAccount, itemId, amount)` | Discovery/quotes, seller location, reservations and structured failure/results |
| Inventory | Quantities keyed by item ID | Authoritative consume/transfer/reserve capabilities for citizen meals and cooking |
| Finance | Wallet balance and mutation service; Vault accounts | Citizen account binding, atomic multi-resource transactions, optional budgets, credit and escrow |
| Property/Housing | Ownership operations, property events, occupant lists | Listings, prices, paid closing process, occupancy allocation and household policies |
| Travel/social/cooking | No complete contracts identified in the inspected modules | Route/duration estimates and arrival, bilateral interactions, accessible recipe execution |

The current Needs system increases Hunger/Thirst yet kills citizens when either is <= 0, while those fields default to zero; Energy decreases from zero as well. Do not build behavioral thresholds around this. Establish initial values, direction, bounds and death thresholds with the Needs owner first. The brain should not silently repair another mod's physiology.

`IBusinessService` currently only creates a business. `CreateBusiness` is not a complete founding transaction and cannot serve as proof of employment or operational readiness. A workforce definition indicates desired staffing, not that a live business has an open, funded job. `OpeningHours` belongs to the business type and does not describe a particular worker's shift.

## Adapter contract shape

A capability descriptor declares ID, version, read/query or command role, input/output types, fact/event keys, optional availability and owning domain. Brain initialization checks all definitions against registered descriptors. Missing optional capabilities disable dependent content with a diagnostic; missing required core capabilities fail activation. Never invent successful fallback outcomes.

Read snapshots carry `revision`, `observedAt`, stable entity IDs and explicit units. Commands carry `operationId`, actor, target, expected revision when relevant, parameters and deadline. Results are `pending`, `succeeded` or `failed` with a machine-readable reason; pending commands complete by query/event. Expected failures such as `out_of_stock`, `insufficient_funds`, `offer_expired`, `shift_revised`, `not_qualified`, `access_denied`, `participant_declined` and `unavailable` are normal control flow.

The executor reconciles accepted operations across event delivery and saves. The event bus provides Publish/Subscribe but no durable or exactly-once guarantee; domain command IDs and result storage must supply deduplication. Subscribe on system start and dispose on stop. A handler must not treat a queued structural ECS change as immediately visible.

## Hiring and actual work hours

Proposed **Employment capability**, owned by Business or a later Employment mod:

```text
SearchVacancies(actor, filters, cursor, limit)
  -> Page<VacancySnapshot>
VacancySnapshot:
  vacancyId, employerId, locationId, roleId, qualificationRequirements,
  payRate { amount, currency, per }, proposedShiftPattern,
  availablePositions, applicationDeadline, revision, observedAt

SubmitApplication(operationId, actor, vacancyId, expectedRevision)
  -> Pending<ApplicationId> | Rejected(reason)
GetApplication(applicationId) -> Pending | Rejected | OfferSnapshot
OfferSnapshot:
  offerId, actorId, employerId, wage, proposedStart, shiftTerms,
  noticeTerms, expiresAt, revision
AcceptOffer(operationId, actor, offerId, expectedRevision)
  -> EmploymentContractSnapshot | Rejected(reason)

GetCurrentContracts(actor) -> list<EmploymentContractSnapshot>
GetShifts(actor, fromInstant, toInstant) -> list<ShiftSnapshot>
ShiftSnapshot:
  shiftId, contractId, locationId, startInstant, endInstant,
  breakRules, attendancePolicy, status, revision

RequestLeave(...), Resign(..., desiredEffectiveDate), CheckIn(...), CheckOut(...)
  -> confirmed result or pending result with operationId
```

The domain derives vacancy count from desired staffing minus active workers and reserved offers, then checks budget and business status. It enforces qualifications and makes hiring decisions. Reserving an offer slot and accepting it must be atomic so two applicants cannot both occupy one opening. The domain issues shifts per contract, including exceptions, holidays, leave, breaks and overnight work. It publishes proposed events such as `VacancyChanged`, `ApplicationResolved`, `EmploymentChanged`, `ShiftChanged`, `AttendanceRecorded` and `WagePaid`.

End-to-end example:

1. An unemployed citizen checks public listings. The adapter maps a vacancy into a belief and a grounded application action. The planner also sees the travel and schedule consequences.
2. Submitting an application starts a pending action. Waiting is bounded and does not monopolize the citizen's whole day: retain application state and pursue other intentions until a result arrives.
3. An offer triggers a career review. Acceptance checks affordability of commute, existing obligations and notice terms. The domain confirms a contract; only then does `employment.has_job` become true.
4. The schedule sensor asks `GetShifts` over the next seven simulation days. Suppose the confirmed shift is 09:00–17:00, travel is 25 minutes, arrival buffer is 10 minutes. Departure is due at 08:25, even if the shop opens at 08:00.
5. The intention to attend becomes urgent as departure approaches. Food plans that cannot finish before departure are rejected. Travel ends at the workplace, then CheckIn validates time/location and records attendance.
6. Working runs through approved breaks and the shift end. The employment/payroll owner calculates pay from recorded attendance; a GOAP predicted wage never credits an account.
7. A shift revision invalidates the cached departure time. Firing removes future work commitments after authoritative confirmation. Resignation retains shifts until its effective end.

## Calendar and travel

Proposed **Time capability** returns an absolute game instant (integer simulation minutes or finer), calendar date, weekday and local minute-of-day, plus conversion functions from simulation ticks. Store durations separately from instants. Do not parse the current time string throughout the brain or assume `SimulationTime.Elapsed` equals calendar time; define that mapping in one adapter. Preserve the existing module's spelling `Calender` when referencing its actual code.

Proposed **Travel capability** exposes estimates with uncertainty and a command for movement. Estimates require origin, destination, transport access, start time and route revision. A missing path is infeasible; zero travel is valid only for the same location or an explicitly declared abstract-world model. Arrival must be confirmed. Closing times and commute estimates are checked again after delays.

## Food and money

Proposed capabilities: `food.find_meal_offers`, `food.find_ingredient_offers`, `food.find_recipes`, `inventory.consume`, `cooking.prepare`, `market.purchase`, `finance.get_spendable` and `needs.apply_consumption`.

Meal offers include item, portion, price, seller/location, availability window, nutritional effect and quote expiry. Ingredient/recipe queries bind actual catalog IDs; behavior YAML uses semantic tags such as `ready_meal` rather than assuming an unverified item ID exists. Cooking checks recipe, equipment, access, ingredients and elapsed time. Consumption removes real inventory and applies the confirmed physiological effect exactly once.

`IMarketService.TryBuy` can back an early purchase adapter, but its boolean result has limited diagnostics and it uses `VaultComponent.Account`, not `WalletComponent`. Do not pass a wallet as a bank account or assume every citizen has a payment account. The adapter needs explicit citizen-to-account lookup. Quote/discovery remains a separate missing capability. Audit atomicity before relying on this service under contention.

Compare eat-owned-food, buy-ready-meal and buy-ingredients/cook using the same satisfy-hunger goal. Cost, time, fatigue, preferences, stock, equipment, distance and the next commitment change which route wins. No special-case “if working then takeaway” script is necessary.

## Housing and founding

Proposed **Housing capability** supports discovery, inspection, financing where available, offer, paid closing and occupancy. `TransferOwnership` alone does not perform a purchase: it must be paired with validated payment and ownership transfer in one domain transaction. Handle competing buyers, listing expiry and capacity.

Proposed **Enterprise capability** supports feasibility estimates, funding allocation, premises control, equipment procurement and a single validated registration/opening process. It can eventually call Business creation internally, but only after domain prerequisites are met. Approval to plan here does not authorize implementing these changes outside this folder.

## ECS placement and dependencies

Register proposed BrainDefinitionRegistry, BrainCapabilityRegistry and brain components during `OnLoad`; register systems through `IModContext.Systems`. Resolve optional domain adapters by explicit capabilities. On `CitizenCreated`, queue initial brain state; initialize existing living citizens once as well. On `CitizenDied`, cancel actions, unsubscribe citizen-specific observations, clean reservations and preserve relevant world history through domain owners.

Proposed logical order: confirmed world results → perception → affect/memory → schedules/projects → decision/planning → execution requests. The exact system priorities and event latency must be agreed with engine scheduling; do not depend on incidental mod load order. Reads and action commands must respect the engine's actual structural command playback points.
