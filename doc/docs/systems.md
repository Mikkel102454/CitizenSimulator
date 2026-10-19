# Systems and queries

## GameSystem

`GameSystem(int priority = 0)` is the base class for scheduled behavior. Smaller priorities run first. The `Priority` property is readable and fixed at construction. `Name` is virtual and defaults to the concrete class name.

`Store` is a protected `IEntityStore`. The scheduler assigns it before `OnStart` and clears it after `OnStop`. Avoid accessing it from the system constructor.

### OnStart(ICommandBuffer commands)

Override this protected virtual callback to cache queries, subscribe to events, and queue initial entities or attachments. The default implementation does nothing. Structural commands play before the next system starts.

### OnUpdate(SimulationTime time, ICommandBuffer commands)

Override this protected virtual callback for tick behavior. Read matching entities through `Store`, mutate existing data directly when appropriate, and queue structural changes. `time` describes the destination simulation time for this tick.

### OnStop(ICommandBuffer commands)

Override this protected virtual callback to dispose subscriptions and release resources. Cleanup callbacks also receive a command buffer. Gameplay events published during shutdown are not automatically followed by another gameplay dispatch.

### Public lifecycle wrappers

`Start(IEntityStore store, ICommandBuffer commands)` validates arguments, assigns `Store`, and calls `OnStart`.

`Update(SimulationTime time, ICommandBuffer commands)` validates the buffer and calls `OnUpdate`.

`Stop(ICommandBuffer commands)` validates the buffer, invokes `OnStop`, and clears `Store` in a finally block.

These wrappers exist for the host scheduler. A mod normally overrides the protected callbacks and lets the scheduler invoke them. Calling wrappers yourself bypasses normal scheduler lifetime management.

## ISystemRegistry

`Register(GameSystem system)` adds a nonnull instance. Registering another instance of the same concrete runtime type throws. Different concrete system types may share a displayed name.

`IsRegistered<T>()` reports whether the exact system type is registered. `GetSystems()` returns a priority sorted list. Equal priorities preserve registration order. The engine caches this list and invalidates its cache when registration succeeds.

The scheduler takes a separate snapshot during startup, so registry changes afterward do not add running systems.

## Query calls

`IQuery.Matches(IReadOnlyDictionary<Type, IComponent> components)` evaluates an entity's component set. Implement this interface for a custom predicate when composition is insufficient. The supplied dictionary contains live component references.

`Query.And(params IQuery[] queries)` creates an `AndQuery` requiring every child to match. An empty list matches every entity.

`Query.Or(params IQuery[] queries)` creates an `OrQuery` requiring any child to match. An empty list matches no entities.

`Query.Not(IQuery query)` creates a `NotQuery` that inverts its child.

You can also construct `AndQuery`, `OrQuery`, and `NotQuery` directly with the same arguments. And and Or copy the supplied array. Null arrays, null children, and a null Not child are rejected.

## Query composition example

Suppose a system needs citizens with needs and without a sleeping marker. Cache this expression during `OnStart` after all three component types have been registered.

```text
Query.And(
    CitizenComponent.GetComponentType(),
    NeedsComponent.GetComponentType(),
    Query.Not(SleepingComponent.GetComponentType())
)
```

To match either of two tags, use this expression.

```text
Query.Or(
    WorkingComponent.GetComponentType(),
    ShoppingComponent.GetComponentType()
)
```

Component queries test presence rather than property values. After retrieving a component, inspect its fields for conditions such as `Alive`, stock quantity, or balance. Alternatively, implement a custom `IQuery` that reads those properties.
