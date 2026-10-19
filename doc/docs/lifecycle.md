# Loading and lifecycle

## Discovery

The host creates `Mods` under `AppContext.BaseDirectory` when necessary. `ModLoader` recursively examines DLL files there and loads them into the default assembly load context. It considers concrete classes implementing `IMod` that have a `ModAttribute`.

The loader uses `Activator.CreateInstance`, so entry points need an accessible constructor without arguments. Assembly and construction errors are logged during discovery. IDs must contain text. ID comparisons are case insensitive. When multiple discovered mods share an ID, the entire conflicting group is excluded.

The assembly resolver maps simple assembly names to DLL paths under `Mods`. Avoid multiple DLLs with the same assembly name because the path map can select only one. There is no isolated load context per mod and no automatic reload facility.

## ModAttribute

`ModAttribute(string id, string name, string version)` supplies identity and display metadata. `Id`, `Name`, and `Version` are readable properties. `Dependencies` is a writable string array containing required mod IDs. The attribute applies to classes and cannot be repeated on the same class.

Dependencies initialize before dependents. Missing dependencies and dependency cycles exclude affected mods from the valid load order. If a dependency throws during `OnLoad`, its dependents are skipped. Unrelated valid mods may still initialize.

The loader registers concrete closed component classes from valid mod assemblies before calling any `OnLoad`. It then calls entry points in dependency order. For otherwise independent mods, traversal begins in sorted ID order. Express ordering requirements with dependencies rather than depending on incidental file names.

Initialization is not transactional. A failed `OnLoad` can leave services, systems, entities, subscriptions, or queued events already registered. No automatic rollback or `IMod` unload callback exists.

## Scheduler lifetime

1. The scheduler snapshots `Systems.GetSystems()` during startup.
2. Systems start in ascending priority order. Equal priorities retain registration order.
3. Each successful tick calls every system's `OnUpdate` in that order.
4. After system updates, the gameplay bus dispatches the events pending at the start of dispatch.
5. Shutdown calls `OnStop` in reverse successful startup order.

Register all systems before scheduler startup. Registering one later changes the registry but does not update the scheduler's captured list.

Each start, update, stop, and event handler invocation receives a fresh command buffer. The world rejects direct structural changes during the invocation. After a successful invocation, queued commands play in submission order before the next invocation begins.

Commands do not wait until the next tick. For example, a component queued by the first system's `OnStart` is available to the second system's `OnStart`.

## Failures and cleanup

If a callback throws, its queued structural commands are discarded. Direct field mutations, service side effects, and published events are not rolled back. If command playback fails, commands already applied remain applied. Commands after the failure do not run.

A failed system startup triggers a cleanup attempt for that system followed by shutdown of already started systems. An update or event delivery failure faults the scheduler. The host reports the exception and attempts shutdown. A failed tick also faults the clock because world state cannot safely be rolled back.

Use `OnStop` to dispose subscriptions and release mod resources. Cleanup should tolerate partial initialization. The `Store` property remains assigned during `OnStop` and is cleared afterward even if cleanup throws.
