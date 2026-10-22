# Delivery status and validation

Updated 2026-10-22. Already implemented planner features are not outstanding tasks.

## Implemented

- Typed facts, handlers, actions, goals, providers, validation and pure expressions.
- Utility/emergency arbitration, bounded resumable GOAP, elapsed-time prediction, availability windows and waiting.
- Executor verification, cancellation, fresh candidate checks and bounded retry delays.
- One-off schedules, weekday/overnight daily rules, independent mod sources, occurrence targets, outcomes and future forecasts.
- Food/drink consumption and purchases, home drinking, sleeping and resting.
- Vacancy discovery, qualified hiring, commute, attendance/payroll, resignation and employment commitments.
- Rental/purchase discovery, moving, access checks, projected rent and ownership transfer.
- Demand reporting that includes shortages; planning budgets and diagnostics.

## Validation

```powershell
dotnet build tests/CitizenSimulator.MindChecks --configuration Release -m:1
dotnet run --project tests/CitizenSimulator.MindChecks --configuration Release --no-build
```

These standalone checks use real registrations, startup validation, ECS scheduling, services and citizen plans. They cover rental access, competing tenants/buyers, canceled/stale transactions, drinking/shopping, schedule isolation, invalid updates, overnight/future candidates, prediction purity, attendance/payroll, interrupted work and sleep.

Build the solution and start the simulator with all mods, including Utilities, to validate registration/dependency loading. These checks do not establish population-scale performance or save/load correctness.

## Remaining design work

The illustrative loader in `examples/` (profiles, projects, manifests and versioned hot reload) is not implemented. Runtime embedded goal/action YAML uses schemas documented under `doc/docs/citizen-mind`.

Detailed routes, transportation, cooking plans, remembered knowledge, stress/emotion, social relationships, persistent savings/business projects, notice periods and asynchronous applications are future features. Job acceptance is synchronous. Home purchase is an affordable immediate choice, not a savings project.

Save/load reconciliation with durable operation IDs, in-flight recovery, replay consistency and content migration still need design/tests. Synchronous services and the executor prevent double commits in a live plan; they do not promise exactly-once replay after restart.

Profile planning work, deadlines, memory, contention and missed shifts before setting population targets. Multi-day scenarios should tune affordability, home supply, shifts, utility shortages and survival.
