# Services and events

## IServiceRegistry

The service registry is a shared map keyed by the exact generic type used for registration. It does not construct dependencies or search assignable types.

### Register<T>(T service)

Stores a nonnull service object under `typeof(T)`. `T` must be a reference type. Registering that type twice throws. To expose a public contract, explicitly choose the interface as `T`.

```text
context.Services.Register<IGreetingService>(new GreetingService())
```

Registering only `GreetingService` does not make `Get<IGreetingService>()` work automatically.

### Get<T>()

Returns the object registered under that exact type. Throws `InvalidOperationException` when absent. Use for required dependencies after declaring their provider's mod ID in `Dependencies`.

```text
greeting = context.Services.Get<IGreetingService>()
```

### TryGet<T>(out T? service)

Returns true and the service when registered. Returns false and null when absent. Use for optional integrations, while remembering that an independent provider might load later if no dependency orders the mods.

The registry offers no replacement, removal, or automatic disposal operation. Let a system own resources that need cleanup and dispose them in `OnStop`.

## Event contracts

`IEvent` is an empty marker interface. Define a class or record implementing it for each public event contract. Prefer immutable payloads or captured values when listeners should observe the state at publication time.

Passing a mutable component in an event payload passes the same object reference. Later mutations can change what listeners observe before delivery.

`EventHandler<TEvent>` is a delegate with parameters `TEvent eventData`, `SimulationTime time`, and `ICommandBuffer commandBuffer`. `TEvent` must implement `IEvent`. The handler returns void. Its buffer follows the same lifetime and structural mutation rules as a system callback.

## IEventBus calls

### Publish<TEvent>(TEvent eventData)

Queues a nonnull payload under the exact generic event type. It does not synchronously invoke listeners. Publishing as `IEvent` addresses subscribers for `IEvent`, rather than subscribers for the concrete runtime type. Use the same concrete event type at publication and subscription.

### Subscribe<TEvent>(EventHandler<TEvent> handler)

Registers a nonnull handler for that exact event type and returns an `IDisposable` subscription. Keep the returned object and call `Dispose()` to unsubscribe. Repeated disposal is harmless. Duplicate subscriptions are separate registrations and can cause repeated delivery.

Each subscriber receives a separate command buffer. A successful subscriber's structural changes play before the next subscriber runs.

## Delivery order

Gameplay publication uses `context.Events`. After all system updates, dispatch snapshots and clears pending events. Events in that snapshot run in publication order. For each event, listeners are snapshotted in subscription order.

An event published by a handler is queued for the next dispatch. Gameplay dispatch normally occurs once per simulation tick, so handler generated gameplay events wait until the next tick. A paused simulation does not automatically deliver gameplay events.

A disposed subscription in an existing listener snapshot skips invocation. A new listener added during delivery does not join the current event's snapshot, though it can receive a later event in that dispatch.

An unhandled listener exception faults scheduled delivery. There is no automatic retry or transaction rollback.

## Choosing communication

Use a service when the caller needs an immediate result, for example checking stock or attempting a transfer. Use an event when announcing something to interested listeners, for example that a citizen was created. Use `InputEvents` for user actions that must work without an advancing simulation.

Regular entity store and event bus operations are not concurrent APIs. Keep mod world operations on scheduled callbacks. The host's input dispatcher and control request queue supply their own synchronization for their specific purposes.
