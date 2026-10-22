# CitizenMind implementation and remaining design

Updated 2026-10-22. CitizenMind implements typed registration and startup validation, utility/emergency goal selection, bounded resumable planning, candidate bindings, elapsed-time prediction, action execution/cancellation, live validation, recurring and one-off commitments, commitment forecasts and planning diagnostics.

The API reference is [the CitizenMind documentation](../../doc/docs/citizen-mind/index.md). YAML under `examples/` describes a future behavior-pack format, not loadable runtime configuration.

All implemented actions, goals and goal-action links are authored in each existing mod's `Data` YAML files. `MindDefinitionLoader` loads normal definitions and catalog templates. C# supplies facts, handlers, providers and catalog data, including item effect values; it does not build behavior definitions. Runtime commitment targets specialize existing YAML goals for particular shift occurrences. See [the YAML reference](../../doc/docs/citizen-mind/yaml.md).

## Connected world behaviors

| Domain | Implemented connection |
| --- | --- |
| Food and drinks | Inventory facts, owned-item consumption, catalog effects through Inventory/Items/Needs services |
| Shopping | Seller-specific offers, price ceilings, availability, stock/funds/capacity checks, atomic purchase |
| Needs | Home drinking, sleep and emergency rest; passive changes forecast over action durations |
| Employment | Qualified vacancy selection, job acceptance, commute, attendance, wages, resignation and dated commitments |
| Housing | Owned-home discovery, rentals, available home purchases, move-in actions, amenity facts, projected rent |
| Demand | Real food/drink intentions report shortages even when shelves are empty; forecasts do not report demand |

New citizens receive an active mind and an inventory with 20 abstract carrying units. Existing inventories are preserved. Active minds choose homes through actions; citizens without an active mind retain automatic allocation.

## Abstract access model

There are no coordinates or routes. Shopping is a five-minute remote order/handover from an accessible retail seller. Working requires a commute (15 minutes by default), arrival, a contract and check-in during the agreed shift intersected with business hours. Moving takes 15 minutes; buying and moving takes 30. Home amenities require a usable residence; metered water/electricity must actually be available when Utilities is loaded. Ownership and occupancy remain separate.

## Mutation boundary and retries

Facts, candidate snapshots and predictors only read the world and produce copied planning state. Handlers call authoritative services at execution. Food is consumed at completion, purchases exchange real funds and goods atomically, and wages follow actual attendance. Cancellation before commit creates no goods, payment, lease or ownership change. Canceling work checks the citizen out.

Live candidates are checked again before execution. Transaction services recheck stock, price, access, funds and capacity. A quoted market price is a maximum: increases reject the offer; decreases can execute at the lower price. Rental/sale IDs distinguish replaced listings. Competing buyers/tenants are resolved synchronously by domain services. Failed plans use the five-game-minute retry delay and rebuild with fresh offers. Completed executor steps are not executed again. Durable transaction IDs across save/load or network retries remain future work.

## Confirmed shifts

Business publishes eight days of upcoming confirmed shifts through `SetScheduleSource` with source `business.employment`. Manual schedules, daily rules and other mod sources are preserved. Every shift binds its contract and full start/end times, including overnight shifts. Completion targets the domain's last completed shift end, preventing tomorrow's shift from appearing satisfied by today's attendance. Preparation includes commute plus one action boundary. Dismissal/suspension withdraws current and future commitments. Outcomes are retained for seven days. Safety emergencies can interrupt work; only attended time earns wages.

See [integrations.md](integrations.md) for adapter ownership and [delivery-plan.md](delivery-plan.md) for validation and remaining scope. Architecture/configuration documents distinguish runtime facilities from longer-term proposals.
