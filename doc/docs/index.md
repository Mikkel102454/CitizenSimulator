# Make a mod

CitizenSimulator separates contracts in `CitizenSimulator.Abstractions` from implementations in `CitizenSimulator.Engine`. A mod implements `IMod`, supplies a `ModAttribute`, and receives an `IModContext` during loading. The context provides the world, component registry, system registry, service registry, simulation clock, queued controls, and two event buses.

The host discovers assemblies under `Mods` beside the executable. It resolves dependencies, registers component types, calls mod entry points, and starts the registered systems. Simulation ticks update systems and then deliver gameplay events. Input events also run between tick batches so controls remain usable while paused.

## Reading order

1. [Build a mod](tutorial.md) explains project setup, entry points, registration, deployment, and a complete behavior recipe.
2. [Loading and lifecycle](lifecycle.md) explains discovery, dependencies, callback ordering, and cleanup.
3. [Entities and components](entities.md) covers every entity and command buffer call.
4. [Systems and queries](systems.md) covers system callbacks and query composition.
5. [Services and events](communication.md) covers communication between mods.
6. [Time and input](timing.md) covers every clock property, control request, and input event.
7. [Troubleshooting](troubleshooting.md) explains common failures and verification.
8. [CitizenMind](citizen-mind/index.md) covers citizen decisions, mod integration, goal and action YAML, and schedules.
9. [Business operation and production](business.md) covers setup, employment, production, procurement, utilities, accounting, and lifecycle.
10. [Coding conventions](coding-conventions.md) explains formatting, naming, and readability expectations for new code.

## Context properties

`IModContext` lives in `CitizenSimulator.Abstractions.Modding`. Its properties expose shared objects rather than separate worlds for each mod.

### Services

`IServiceRegistry` shares typed service objects. Register your public interface during `OnLoad`, then retrieve a dependency through its registered interface type.

### Store

`IEntityStore` reads entities and components. Direct structural changes are available before systems start. During callbacks, use the supplied command buffer for creation, destruction, attachment, and removal.

### Components

`IComponentRegistry` registers component types and returns query tokens. Component registration describes a type. Attaching a component creates data on an entity. These are separate operations.

### Systems

`ISystemRegistry` collects `GameSystem` instances. Register systems during `OnLoad` so the scheduler includes them in its startup snapshot.

### Events

`IEventBus` queues gameplay events for delivery after system updates. Events published by a gameplay event handler wait for the next gameplay dispatch.

### InputEvents

A separate `IEventBus` delivers input even when the simulation is paused. Subscribe here for keyboard events, submitted console lines, and control failures.

### Clock

`ISimulationClock` exposes simulation state for inspection. Use the `SimulationTime` supplied to a callback for the destination time of the tick currently executing.

### Controls

`ISimulationControls` queues requests that the host applies between ticks. A request does not immediately change the clock.

## Using this guide

Start with the tutorial to create your own component, system, and event. Use the reference pages when implementing loading, world changes, communication, or input.

Call sequences are pseudocode. Translate them into valid C# when implementing your mod. XML project examples are usable as written.
