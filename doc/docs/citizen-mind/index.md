# CitizenMind

CitizenMind chooses goals for citizens, searches for action plans, and runs those plans through handlers supplied by mods. YAML describes decisions and predicted outcomes. Handlers and world services perform the actual activity.

The mod ID is `citizen_mind`. Its assembly is `CitizenSimulator.CitizenMind`, and it depends on the calendar mod whose ID is `calender`. The spelling of these IDs matters when declaring dependencies.

## Reading order

1. [Processing](processing.md) explains goal selection, planning, execution, and time.
2. [Mod API](api.md) explains registration, facts, handlers, candidate providers, and time predictors.
3. [Goals and actions in YAML](yaml.md) covers loading, definition fields, expressions, and complete examples.
4. [Schedules](schedules.md) explains one-off commitments, recurring rules, priorities, and outcomes.

These pages describe the implemented APIs. The files under `CitizenSimulator.CitizenMind/plan` track implementation status and longer-term proposals; their illustrative behavior-pack YAML is not a configuration reference for the current loader.

## Connected behaviors

Inventory registers eating and drinking for consumables, and Market adds seller-bound shopping to the food/drink goals. Needs supplies home drinking, sleeping and emergency rest. Business supplies job search, qualified acceptance, commute, work, resignation and confirmed shift commitments. Housing supplies owned-home, rental and purchase choices with move actions. Real changes remain in those domain services; predictions only update copied facts.

The world uses abstract access. Shopping takes five game minutes without a map route. Work requires a commute (15 minutes by default), arrival and a valid shift. Moving takes 15 minutes; purchase plus move takes 30. Home amenities require residence and usable facilities, including actual metered utilities when loaded. Ownership does not imply occupancy.

Failed purchases or moves retry through fresh planning after the five-game-minute cooldown. Domains recheck stock, funds, quoted price, current listing identity, opening hours and capacity at execution. No goods or money are created by forecasts. Cancellation before commit is free of transaction effects; interrupting work ends paid attendance.

## Give a citizen a mind

`CitizenService.CreateCitizen` attaches an active `MindComponent` automatically. Inventory also initializes carried storage with 20 abstract units of capacity, preserving any existing inventory. For other entity types, attach a `MindComponent` from `CitizenSimulator.CitizenMind.Components` using the entity and command buffer rules in [Entities and components](../entities.md). It starts with `IsActive = true`.

`CurrentGoal`, `CurrentCommitment`, and `Execution` expose the current activity for inspection. Use `ICitizenMindService` for schedule changes. Leave retry and emergency bookkeeping to CitizenMind.

Setting `IsActive = false` causes cleanup on the next mind update, including cancellation of running work and removal of schedules and daily rules. Removing the component and stopping the system also clean up mind work. Reactivating requires restoring manual schedules; employment commitments are rebuilt from current contracts. Citizens without an active mind retain Housing's automatic allocation; active minds choose housing through actions.

## Integration recipe

During your mod's `OnLoad`, retrieve `ICitizenMindService`, register its facts, candidate providers and handlers, then load its embedded YAML through `MindDefinitionLoader`. Actions, goals and goal-action links must always be authored in YAML. Catalog templates keep item-specific behavior in YAML too. Before systems start, CitizenMind validates the combined definitions and closes registration.

After attaching a mind, assign any schedules through the runtime API. Read the [mod API](api.md) before implementing handlers: prediction changes a planning snapshot and never applies effects to the live world.
