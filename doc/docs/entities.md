# Entities and components

## EntityRef

`EntityRef(int Id, int Generation, int StoreId)` is a value identifying an entity within a store. Equality includes all three fields. Do not construct references from a guessed ID. The current implementation issues increasing IDs with generation `1` and assigns each store its own identifier.

The default value is a detached or absent reference. A reserved entity returned by a command buffer is also not alive until its create command plays. Use `Store.IsAlive(entity)` before reading a reference that might have been destroyed.

## Component contracts

`IComponent` exposes `Parent`. Its setter is internal to the contract. `Component<T>` implements this contract and gives derived components a publicly readable parent. Prefer deriving from `Component<YourComponent>` rather than implementing parent bookkeeping yourself.

The engine assigns `Parent` during direct attachment or when attachment is queued. A queued parent therefore does not prove that the component is already in the store. Removal, entity destruction, or discarded unattached commands clear the parent to its default value.

A component instance cannot belong to different entities simultaneously. Use a fresh instance for each entity. Components are stored by the exact generic type supplied to `AddComponent<T>`, so use the concrete component type consistently.

## IEntityStore calls

### CreateEntity()

Returns a new active `EntityRef` immediately. Use during mod loading or other host controlled setup outside scheduled callbacks. During callbacks call `commands.CreateEntity()` instead.

### DestroyEntity(EntityRef entity)

Immediately removes a live entity and clears all attached component parents. Invalid or destroyed references throw. Use the command buffer during callbacks.

### IsAlive(EntityRef entity)

Returns whether the reference belongs to this store, has the expected generation, and currently identifies an active entity. Returns false for pending reservations, destroyed references, and foreign references.

### AddComponent<T>(EntityRef entity, T component)

Immediately attaches and returns the same component instance. `T` must be a class implementing `IComponent`. The entity must be alive, the instance must be nonnull, and the entity must not already contain this exact component type. Attaching a duplicate throws rather than replacing data. During callbacks use the command buffer.

### GetComponent<T>(EntityRef entity)

Returns the component or null when that type is absent on a live entity. An invalid entity throws rather than returning null. Reading returns the stored reference, so changing its properties changes shared state immediately.

### HasComponent<T>(EntityRef entity)

Reports whether a live entity contains the exact component type. An invalid entity throws. This call cannot see attachments waiting in a command buffer.

### RemoveComponent<T>(EntityRef entity)

Immediately removes the type when present and clears the detached component's parent. Missing components are harmless on a valid entity. Invalid entities throw. Use the command buffer during callbacks.

### Query(IQuery query)

Lazily enumerates active entities whose component dictionaries match the query. A null query throws. Avoid direct structural mutation while enumerating. Queue structural work for the end of the callback instead.

## ICommandBuffer calls

Every method operates on the current callback's buffer. Never retain the buffer for another callback, another thread, or later asynchronous work. Playback and discard close it permanently.

### CreateEntity()

Reserves and returns an `EntityRef`, then queues activation. You may queue component attachments to this reference in the same buffer. Store reads still reject it until playback activates it.

### DestroyEntity(EntityRef entity)

Queues strict destruction. The reference must belong to this store and must not be reserved by another buffer. Liveness is enforced when the command executes. Destroying an already destroyed entity causes playback failure.

### TryDestroyEntity(EntityRef entity)

Queues destruction only if the entity is alive when playback reaches the command. Useful when multiple operations might destroy the same entity. Foreign references and reservations owned by another buffer still throw when queued. This is not a universal validation bypass.

### AddComponent<T>(EntityRef entity, T component)

Queues attachment and immediately returns the supplied object. `Parent` is assigned when queued, so you can configure the returned component before attachment. Duplicate type checks happen during playback. Repeated additions of the same type can therefore fail after earlier commands have already succeeded.

### RemoveComponent<T>(EntityRef entity)

Queues removal. Missing types are harmless at playback if the entity is alive. Removing from an entity destroyed by an earlier command fails validation.

## Creating related state

This is pseudocode for a callback that creates an entity and attaches data.

```text
entity = commands.CreateEntity()
counter = commands.AddComponent(entity, new CounterComponent { Count = 0 })
counter.Count = 5
```

Do not call `Store.GetComponent<CounterComponent>(entity)` in this sequence. The reference is still reserved. Retain the returned component object for work within this callback and read through the store in later callbacks.

## Component registry calls

`IComponentRegistry.Register<T>()` registers the type and returns `ComponentType<T>`. Registering the same type again returns its existing token.

`IComponentRegistry.Get<T>()` returns an existing token and throws when the type is unregistered. `IComponentRegistry.IsRegistered<T>()` checks registration without throwing for absence.

`ComponentTypes.Initialize(IComponentRegistry registry)` sets the global registry used by static helpers. The host calls it before loading mods. Mods should use the supplied registry rather than reinitialize this shared state.

`ComponentTypes.Get<T>()` forwards to that registry and throws if initialization has not occurred or the type is absent. `Component<T>.GetComponentType()` is the convenient inherited form.

`ComponentType<T>(int Id)` implements `IQuery`. Its `Matches` method checks the presence of `typeof(T)` in a component dictionary. The numeric ID is registry metadata rather than a persistent identifier to save or transmit.
