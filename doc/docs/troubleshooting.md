# Troubleshooting

## The mod never appears

Confirm that its DLL is below `Mods` beside the executable being run. A folder named `Mods` at the repository root is not automatically searched. Check for a concrete `IMod` implementation, a `ModAttribute`, and a public constructor without arguments. Read discovery errors for missing assemblies or incompatible binaries.

## Duplicate ID or missing dependency

IDs are case insensitive. All entries in a duplicate ID group are excluded. Dependencies contain mod IDs rather than project names or assembly names. Install each required provider DLL and remove cycles from dependency declarations.

## A service is not registered

Check the exact generic registration type. A concrete class registration does not satisfy an interface lookup. Required providers must initialize first through a declared dependency. Some modules, including the current brain entry point, do not register every service class present in their source tree.

## A system does not execute

Register an instance through `context.Systems.Register` during `OnLoad`. Component discovery does not register systems. Registration after scheduler startup does not affect the captured system list. A fully paused simulation does not call `OnUpdate` until stepped, advanced, or resumed.

## Structural mutation throws

Entity creation, destruction, component attachment, and removal must use the callback's command buffer during system and event callbacks. A service can also trigger this error if its implementation directly changes structure. `ICitizenService.CreateCitizen` currently takes that direct path.

## A new entity is invalid

A reference returned by `commands.CreateEntity()` is reserved until playback. Attach data through the same buffer and use the returned component object within the callback. Read it through the store only after the callback succeeds and playback completes.

Services that call `Store.HasComponent` before queueing an attachment also require an already live entity. Their signatures accepting a command buffer do not imply support for a pending entity.

## An event seems delayed or missing

Check the bus and generic event type. Gameplay events dispatch after systems and wait while paused. Events published by gameplay handlers wait for another dispatch. Input subscriptions belong on `InputEvents`. Keep subscriptions alive and dispose them only at the end of their intended lifetime.

## A control request has no immediate effect

Controls are applied later in submission order. Inspect subsequent clock state or listen for `ControlFailed`. Pause before stepping or advancing. Pause again to cancel an active target before requesting another manual advance or resuming.

## Partial changes remain after failure

Command playback is ordered without rollback. Earlier commands can succeed before a later command fails. Direct data changes and service effects are also not rolled back when a callback throws. Validate inputs before mutation and avoid duplicate queued attachments.

## Verification recipe

1. Build the host and mod against the same abstraction assembly.
2. Install the mod and all required providers under the executable's `Mods` directory.
3. Confirm successful load messages for every dependency and the mod itself.
4. Check one startup operation and one update operation with observable output.
5. Confirm that a command from an earlier system is visible to a later system in the same tick.
6. Pause and verify that submitted input still reaches its listener.
7. Step once and confirm that the simulation performs exactly one update per registered system.
8. Quit and confirm subscription cleanup and system shutdown.

## Documentation maintenance

Keep changes to this documentation inside `doc`. Run `mkdocs build` from that folder to validate navigation and generate the site. The supplied configuration keeps output in `doc/site` and uses the built in MkDocs theme.

The primary implementation references are `CitizenSimulator.Abstractions`, `CitizenSimulator.Engine/ECS`, `CitizenSimulator.Engine/Events`, `CitizenSimulator.Engine/Modding`, `CitizenSimulator.Engine/Timing`, `CitizenSimulator.Engine/Input`, and each gameplay provider's `Services` directory.
