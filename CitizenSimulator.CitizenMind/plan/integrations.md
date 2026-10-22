# Current world integration

Updated 2026-10-22. These are implemented adapters.

| Owner | Main registration/service | Responsibility |
| --- | --- | --- |
| CitizenMind | `CitizenMindService`, `CitizenMindSystem` | Validation, selection, planning, forecasts and execution |
| Citizen | `CitizenService` | Creation/death and attaching minds |
| Needs | `Needs.OnLoad`, handlers and passive predictor | Hunger, thirst, energy, home drinking, sleep and rest |
| Inventory | `FoodActions`, `CitizenInventorySystem`, `IInventoryService` | Stock, eating/drinking, inventory initialization and consumption |
| Items | `ConsumptionEffects`, `ConsumptionService` | Catalog effects applied through NeedsService |
| Vault | `AccountFacts`, `IVaultService` | Accounts/transfers; canonical predictable balance fact `market.balance` |
| Market | `ShoppingActions`, `OfferCandidateProvider`, `BuyItemHandler` | Bound offers, execution checks and real purchase |
| Market | `CitizenDemandSystem` | Food/drink requests, shortages, substitution and demand cancellation |
| Business | `EmploymentActions`, job/shift providers | Qualified vacancy selection, acceptance, travel, work and resignation |
| Business | `EmploymentScheduleSystem`, `IBusinessService` | Confirmed shifts, attendance proof, labor and payroll |
| Housing | `HousingActions`, `HousingCandidateProvider`, `MoveHomeHandler` | Affordable owned/rented/purchased homes and deferred move/closing |
| Housing | `IHousingService`, `HousingRentPredictor` | Real occupancy/leases/transactions and pure projected rent |
| Utilities | `IHomeUtilityService` | Real water/electricity availability for home access |

## Planning reads, execution writes

Candidate factories capture current choices. Planning can explore them without changing stock, money, contracts, wages or leases. Predictions modify declared predictable facts in copied state. Never issue transfers, imports, demand reports, leases or employment calls from predictors.

Handlers validate live entities and bound identities again. `MoveHomeHandler` checks current listing identity/price, vacancy and affordability; the service validates again at commit. Home purchase requires space for the buyer after owner occupancy ends and preserves existing tenants.

`BuyItemHandler` rechecks seller and price ceiling during its five-minute action. Market atomically validates and transfers payment/stock at completion. Channels, opening hours and service capacity remain domain rules. Empty shops still receive unmet demand.

`WorkShiftHandler` binds contract identity and exact shift times. Travel establishes workplace access; work checks in. Actual attendance accrues wages and work/service contribution. Interruption calls `TryLeaveWorkplace`. Completion requires actual attendance extending to the occurrence's end. Partial attendance through the end can complete the commitment; payroll reflects only attended minutes. Predictions never pay wages.

## Time and ordering

One game tick is five minutes (288 ticks/day). Business advances at priority -5, Market at -4, demand at -3, and employment schedules at -2, before CitizenMind at 0. Housing also advances before minds. Calendar supplies absolute game time; overnight windows retain their start date.

World changes between planning and execution are expected. Failed execution cancels temporary activity and searches after the retry cooldown with fresh facts/candidates. A projected plan does not reserve a transaction. The single-threaded scheduler serializes commits. Persistence-level idempotency, asynchronous applications, detailed spatial travel and bilateral social transactions remain future extensions.
