# CitizenMind mod API

## Retrieve the service

Reference the `CitizenSimulator.CitizenMind` project or assembly and declare `citizen_mind` in your mod's `Dependencies`. Include dependencies for any other providers your definitions require. Retrieve the service in `OnLoad`.

```csharp
using CitizenSimulator.CitizenMind.Services.Abstractions;

ICitizenMindService mind = context.Services.Get<ICitizenMindService>();
```

Registration methods are available only before CitizenMind's system starts. Schedule methods remain available at runtime. IDs must be nonblank and unique within each registration category. Prefix your IDs with your mod ID to avoid collisions. There is no unregister or replacement API for definitions and providers.

`SetScheduleSource(EntityRef, string sourceId, IReadOnlyList<ScheduledCommitment>)` updates one mod's runtime commitments without replacing manual, daily or other sources. `ScheduledCommitment.Target` optionally supplies an occurrence-specific registered double fact and minimum threshold. See [Schedules](schedules.md).

Vault registers the predictable decimal balance fact `market.balance` (constant `AccountFacts.Balance`). Shopping and housing use this same fact so projected spending shares one budget. `HousingRentPredictor` subtracts prorated rent from copied planning balances; actual collections remain in HousingService. Do not register another balance alias for another purchase action.

## RegisterFact<T>(string id, Func<EntityRef, T> provider, bool canPredict = false)

Registers a live observation for a citizen. The declared generic type becomes the fact's value type, with nullable value types unwrapped. Providers may be called repeatedly during selection, planning, and verification. Keep them cheap and free of world mutations.

This example assumes `ReadFatigue` is your mod's observation function returning a `double`.

```csharp
mind.RegisterFact<double>("example.fatigue", ReadFatigue, canPredict: true);
```

`canPredict: true` permits prediction to change this fact in copied planning state. It does not make the fact writable through CitizenMind. Read-only observations such as access permissions can keep the default `false`. Return null for unknown observations when appropriate; a null boolean evaluation is false and arithmetic involving unknown values propagates unknown values.

## RegisterGoal and RegisterAction

`RegisterGoal(GoalDefinition goal)` adds a goal. `RegisterAction(ActionDefinition action)` adds an action and parses its typed parameters immediately. Most definition classes are in `CitizenSimulator.CitizenMind.Models`; parameter and provider descriptor types are in `.Definitions`.

Definitions may be constructed in C# or deserialized using the [YAML recipe](yaml.md). Cross-references are validated at system startup, so registration order between individual goals, actions, facts, and handlers does not need to mirror their references. Finish all registration before startup and treat registered definitions as immutable.

## RegisterHandler(string id, IMindActionHandler handler)

`IMindActionHandler` lives in `CitizenSimulator.CitizenMind.Models.Abstractions` and has three calls.

| Call | Responsibility |
| --- | --- |
| `ActionResult Start(ActionContext context)` | Check live prerequisites, acquire resources if needed, and begin work. |
| `ActionResult Tick(ActionContext context)` | Apply real progress using game delta and report the result. |
| `void Cancel(ActionContext context)` | Release remaining reservations and temporary resources. |

Return `RUNNING`, `SUCCEEDED`, or `FAILED`. Completion expressions verify reported success. Release resources on successful completion too: the executor does not call `Cancel` after verified success. Cancellation must also be safe after reported failure or success that fails verification, and must not undo committed world changes.

`ActionContext` contains `Citizen`, `Elapsed`, `Delta`, `GameTime`, parsed `Parameters`, and candidate `Bindings`. `GetParameter<T>(name)` requires the exact parsed type: `BOOLEAN` becomes `bool`, `INTEGER` becomes `int`, `DOUBLE` becomes `double`, and `DECIMAL` becomes `decimal`.

One registered handler can serve many citizens. Keep per-citizen action state separate, keyed by `EntityRef`, or use world services designed for that purpose. Callbacks do not receive an ECS command buffer. Use your mod's service or queued work for structural changes according to the engine's callback rules.

## RegisterCandidateProvider

`RegisterCandidateProvider(CandidateProviderDescriptor descriptor, Func<EntityRef, IActionCandidateProvider> factory)` declares binding names and their CLR types, plus a factory for a citizen's provider. Set an action's `candidateProvider` to the descriptor ID.

`IActionCandidateProvider.GetCandidates(ActionDefinition definition, EvaluationContext state)` returns an `IReadOnlyList<ActionCandidate>`. Each candidate has an ID, its definition, bindings, and optional availability windows. Use stable IDs and binding values that agree with the descriptor's types. Candidate expressions can then read values such as `{binding: price}`. The handler receives the same bindings for live execution.

Factories and candidate calls are part of planning and validation. Capture observations for the search, use the supplied projected state for decisions, and avoid side effects. Do not reserve stock, spend money, or move entities while enumerating choices.

`Availability` contains absolute `TimeWindow` entries. `DailyAvailability` contains recurring `DailyTimeWindow` entries. Null means unrestricted for that category; an empty list means unavailable. If both categories are supplied, both must allow the entire action duration. Actions without a provider use the planner's default unbound candidate.

## RegisterTimePredictor

`RegisterTimePredictor(string id, IElapsedTimePredictor predictor)` adds passive world dynamics to planning. `Predict(IReadOnlyDictionary<string, object?> facts, TimeSpan gameTime, TimeSpan elapsed)` returns only changed facts.

Every predictor reads the same starting snapshot. Their changes are combined before action completion effects are evaluated. Updated facts must be registered and predictable, and two predictors cannot update the same fact. Omit unknown facts to preserve their unknown state. Implementations must be deterministic and must not mutate observations, ECS state, or world services.

The owning mod must still apply passive changes in its live system. Sharing the same calculation between that system and its predictor helps keep plans consistent with execution.

## Runtime schedules

`SetSchedule`, `GetSchedule`, `ClearSchedule`, `SetDailySchedule`, `GetDailySchedule`, and `ClearDailySchedule` all require a live citizen with an attached `MindComponent`. See [Schedules](schedules.md) for examples and replacement semantics. Invoke world and schedule operations on the simulation's scheduled callbacks, following the [service rules](../communication.md).
