# CitizenMind

CitizenMind chooses goals for citizens, searches for action plans, and runs those plans through handlers supplied by mods. YAML describes decisions and predicted outcomes. Handlers and world services perform the actual activity.

The mod ID is `citizen_mind`. Its assembly is `CitizenSimulator.CitizenMind`, and it depends on the calendar mod whose ID is `calender`. The spelling of these IDs matters when declaring dependencies.

## Reading order

1. [Processing](processing.md) explains goal selection, planning, execution, and time.
2. [Mod API](api.md) explains registration, facts, handlers, candidate providers, and time predictors.
3. [Goals and actions in YAML](yaml.md) covers loading, definition fields, expressions, and complete examples.
4. [Schedules](schedules.md) explains one-off commitments, recurring rules, priorities, and outcomes.

These pages describe the implemented APIs. The files under `CitizenSimulator.CitizenMind/plan` contain design proposals and are not a configuration reference for the current loader.

## Give a citizen a mind

Attach a `MindComponent` from `CitizenSimulator.CitizenMind.Components` to the citizen using the entity and command buffer rules in [Entities and components](../entities.md). It starts with `IsActive = true`. The system processes entities with this component; it does not attach minds automatically.

`CurrentGoal`, `CurrentCommitment`, and `Execution` expose the current activity for inspection. Use `ICitizenMindService` for schedule changes. Leave retry and emergency bookkeeping to CitizenMind.

Setting `IsActive = false` causes cleanup on the next mind update, including cancellation of running work and removal of schedules and daily rules. Removing the component and stopping the system also clean up mind work. Reactivating a mind therefore requires restoring any schedules you need.

## Integration recipe

During your mod's `OnLoad`, retrieve `ICitizenMindService`, register the facts and handlers your definitions reference, then register goals and actions. Before systems start, CitizenMind validates the combined definitions and closes registration.

After attaching a mind, assign any schedules through the runtime API. Read the [mod API](api.md) before implementing handlers: prediction changes a planning snapshot and never applies effects to the live world.
