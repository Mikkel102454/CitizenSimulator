# Planning diagnostics

Call `CitizenMindSystem.GetPlanningDiagnostics()` on the simulation thread, for example
from a debugger after an update. It returns a detached snapshot; collecting one does
not reset the counters. There is no automatic logging.

- `PendingRequests`: queued searches and action-boundary validations.
- `WorkLastTick`: deterministic work consumed during the latest planning pass.
- `Succeeded`, `NoPath`, `BudgetExceeded`, `Stale`, `Cancelled`: lifetime request
  outcomes. Success includes goals already satisfied and successful boundary
  validations; it does not mean an action finished executing. Cancellation includes
  superseded requests and cleanup. Stale includes invalidated requests and results
  rejected by fresh validation.
- `RecentRequests`: the last 64 retired requests, oldest first, with citizen, goal,
  outcome and `IsValidation` to distinguish boundary checks from plan searches.
  `QueueWait` measures enqueue to first processing (or cancellation if never run).
  `Lifetime` includes all pauses until retirement. `ActiveTime` adds only processing
  slices, including setup, providers, schedule forecasts and fresh validation.
  These are elapsed wall-clock durations, not simulation time or CPU-only timings.
  `Expansions` includes nested forecasts; `WorkUnits` also includes setup and replay.
- `Providers`: lifetime callback counts and elapsed times grouped by provider ID,
  including work from currently pending requests. `SnapshotTime` measures provider
  factory calls; `CandidateTime` measures `GetCandidates` callbacks in search and
  validation. These durations are already included in request `ActiveTime`.

Equivalent re-enqueues preserve the original request and its measurements. History
is bounded independently of lifetime counters. Read snapshots on the simulation
thread; the scheduler does not support concurrent reads during updates.

Look for high queue waits with a saturated work budget before raising the budget.
High provider time points toward observation/query work. High active time with many
expansions points toward search or state comparisons. Frequent stale outcomes can
mean observations change faster than queued plans finish. Compare wall-clock timings
over several updates because JIT, GC and OS scheduling affect individual samples.

The deterministic work budget still controls planning. Timings are observational
and never change search order or decisions. Provider callbacks remain synchronous.
