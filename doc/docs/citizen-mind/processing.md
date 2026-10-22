# CitizenMind processing

## Startup

CitizenMind registers `ICitizenMindService`, its built-in `citizen_mind.wait_until` handler, and `CitizenMindSystem` during `OnLoad`. It also loads embedded goal and action YAML from its own assembly. Other mods register their contributions during their own `OnLoad` callbacks.

On system start, `MindDefinitionValidator` checks the combined definitions. Missing handlers, facts, actions, or candidate providers cause startup failure. Registration closes after validation; runtime calls to any `Register...` method then throw.

## Each mind update

1. Convert the destination tick into calendar game time and calculate one tick's game duration.
2. For each active mind, generate daily occurrences within the planning horizon, refresh schedule outcomes, and handle finished execution.
3. Select a goal and enqueue any replacement search or schedule validation.
4. Spend the shared planning allowance across queued jobs.
5. Update existing executions. A replacement search can span ticks while an existing action continues.

Requests are gathered before planning work begins, so a later citizen's emergency can take priority over earlier ordinary work.

## Goal selection

A normal goal must be eligible and have at least one unsatisfied desired condition. All `desired` conditions must be true to satisfy it.

Selection checks emergencies first, ordered by highest emergency priority. It next checks pending commitments whose preparation period has started, ordered by highest commitment priority, earliest window end, then commitment ID. Ordinary eligible goals compete by utility.

A consideration maps its input through a piecewise linear curve, clamping to endpoint scores outside the curve. Utility is the weighted average of consideration scores multiplied by `bias`, then clamped to `[0, 1]`. Utility changes alone do not continually interrupt an existing plan. Emergencies, more urgent commitments, and schedule revisions can trigger replacement work.

## Planning

Planning starts from a snapshot of registered facts. `goalActions` supplies seed actions rather than a fixed sequence. Dependency resolution also includes actions that predict changes to facts read by those actions, recursively. For example, a purchase action can become available because an eating action reads inventory.

Candidates supply concrete choices and bindings. The search checks preconditions, estimates duration and cost, advances predicted time and passive changes, and applies predicted action effects to copied state. It checks completion and desired conditions against projected observations. Candidate opening windows and commitment windows constrain the result; the planner can insert waiting steps.

For ordinary work, planning also forecasts pending commitments within the horizon. When planning a commitment, it protects other commitments of equal or higher priority. Emergency planning bypasses this schedule protection. Forecast reservations describe planned feasibility; handlers still own real resource reservations and live availability checks.

Results include `FOUND`, `ALREADY_SATISFIED`, `NO_PATH`, and `BUDGET_EXCEEDED`. A search can remain pending across ticks without a terminal result. Found plans are validated against fresh observations before use. Schedule-sensitive execution requires fresh validation at action boundaries.

The default planner limits are depth `8`, expansions `256`, candidates per action `32`, plan duration one game day, and schedule checks `16`. The planning scheduler defaults to `256` work units per tick and slices of `8`. These are constructor options on the system, not YAML fields or runtime service settings. Provider callbacks are synchronous, so this allowance is not a wall clock limit.

## Execution

At an action boundary, the executor reads live facts and checks the action again. It captures before-action facts and calls `Start`. A `RUNNING` result leads to `Tick` calls on subsequent updates. A successful step advances at most one step in that update.

`SUCCEEDED` only advances the plan if every `completion` condition passes against live observations. `FAILED`, unverified success, and timeout fail the execution and invoke cancellation cleanup for a started action. Prediction never writes world state; the handler must make the real changes.

Normal replacement respects `execution.interruptible`. Failure and shutdown cleanup can force cancellation. A failed goal or terminal unsuccessful search receives a retry delay of approximately five game minutes. A newly activated emergency clears its goal's existing retry delay.

## Time values

Mind durations, availability, schedule windows, and handler elapsed time use calendar game time. They are different from wall time and the engine's raw simulation elapsed time. `ActionContext.GameTime` is absolute game time during `Start` and `Tick`; `Elapsed` and `Delta` describe the current action in game time. `Start` receives zero elapsed and delta.

The current cancellation context supplies elapsed time and zero delta but leaves `GameTime` at its default value. Cleanup should not rely on its game timestamp.
