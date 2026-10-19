# Time and input

## SimulationTime

`SimulationTime(ulong Tick, TimeSpan Delta, TimeSpan Elapsed)` is the callback time value. `Tick` is the destination tick number. `Delta` is the fixed simulation duration of one tick. `Elapsed` is the simulation timestamp at that destination.

Inside a tick callback the shared clock can still report the previously completed tick. The clock commits the destination tick after all work for the tick succeeds. Use the callback argument when calculating current tick behavior.

## ISimulationClock properties

`TickRate` is the configured number of simulation ticks per simulation second. The host defaults to `30`.

`Tick` is the latest committed tick, initially zero. `Delta` is the fixed tick duration. `Elapsed` is total simulation time at the committed tick, rather than wall clock time.

`IsPaused` reports whether normal time accumulation is paused. `Speed` is the positive real time multiplier, initially `1`. Speed changes the pace of ticks while leaving each tick's simulation duration fixed.

`IsAdvancing` reports an active manual target. It can be true while `IsPaused` is also true because manual advancement executes ticks without resuming normal time accumulation.

The host defaults to batches of at most `8` ticks, allowing input and queued controls to be processed between batches. Controls are also applied after each completed tick.

## ISimulationControls calls

Requests are queued and applied in submission order between ticks. Reading the clock immediately after requesting a change may show the old state. The host handles up to `64` queued requests per application pass.

### Pause()

Pauses normal advancement and cancels an active manual target. Use this to establish the state required for stepping or to cancel a long advance.

### Resume()

Resumes normal accumulation. Fails if a manual target is still active. Queue `Pause()` before `Resume()` when cancelling an advance and returning to normal running.

### SetSpeed(double speed)

Sets the real time multiplier. The value must be finite and greater than zero. Zero is invalid, so use `Pause()` to stop advancement.

### Step(ulong count = 1)

Runs the requested positive number of ticks while remaining paused afterward. Requires a paused clock with no active manual target. A count of zero is invalid. The target is calculated from the current committed tick when the queued request is applied.

### AdvanceBy(TimeSpan duration)

Advances relative to the current simulation elapsed time. Requires a paused clock without an active target. Duration must be nonnegative. A duration of zero completes without scheduling extra ticks.

### AdvanceTo(TimeSpan target)

Advances to an absolute simulation timestamp. Requires a paused clock without an active target. The target cannot precede the current elapsed time. A target between tick boundaries rounds upward to the first tick reaching that timestamp. The exposed current timestamp is recognized without adding a tick.

### Quit()

Requests host cancellation. The host exits its main loop and stops systems. This uses the normal shutdown path.

Invalid queued control arguments, invalid state, and arithmetic overflow are reported as `ControlFailed` events on `InputEvents`. Subscribe to these events to display errors. Validation happens when the request is applied, so a try block around the request alone does not receive those failures.

## Input event types

All input events are in `CitizenSimulator.Abstractions.Input` and implement `IEvent`.

`KeyPressed(InputKey Key, KeyModifiers Modifiers)` reports a transition to held state. Repeated native presses while already held do not produce another transition event.

`KeyReleased(InputKey Key, KeyModifiers Modifiers)` reports a transition out of held state.

`KeyHeld(InputKey Key, KeyModifiers Modifiers)` is published once per simulation tick for each held key. It does not continue firing while fully paused. Its modifier value comes from the dispatcher's latest modifier state.

`LineSubmitted(string Text)` reports a complete console line. `ControlFailed(string Message)` reports a rejected queued control operation.

Pressed, released, and submitted line events are drained between batches even while paused. Between batch input callbacks receive a time value with zero `Delta` and the current committed time. Held callbacks run before system updates with the destination tick time.

## Key values

`InputKey` includes `Unknown`, `Enter`, `Escape`, `Space`, `Left`, `Right`, `Up`, `Down`, `Backspace`, `Tab`, `Shift`, `Control`, `Alt`, letters `A` through `Z`, and `F1` through `F12`.

`KeyModifiers` is a flags enum. `None` is `0`, `Shift` is `1`, `Control` is `2`, and `Alt` is `4`. Combine flags when matching chords.

## IInputState.IsDown(InputKey key)

Retrieve `IInputState` through `context.Services.Get<IInputState>()`. The host registers it before loading mods. `IsDown` reports whether the dispatcher currently considers the key held. `Unknown` is not added to held state.

Native keyboard input is available through the Windows console adapter when it can be created. Otherwise the host falls back to submitted console lines, so keyboard transition events should not be assumed available on every host.

## Pause binding recipe

During a system's `OnStart`, subscribe to `KeyPressed` on `context.InputEvents` and keep the returned subscription. The handler can use this pseudocode.

```text
if data.Key == InputKey.Space
    if context.Clock.IsPaused
        context.Controls.Resume()
    else
        context.Controls.Pause()
```

For a manual advance, enqueue calls in this order.

```text
context.Controls.Pause()
context.Controls.AdvanceBy(TimeSpan.FromSeconds(10))
```

Dispose the binding subscription during `OnStop`. Subscribe to `ControlFailed` to report errors if a request conflicts with another queued control.

## Console controls mod

The optional mod with ID `console` translates submitted lines into controls. Its commands are `pause`, `resume`, `speed <multiplier>`, `step [count]`, `advance <seconds>`, `until <seconds>`, `status`, `help`, and `quit`. `until` uses absolute simulation elapsed seconds. Parsing uses invariant culture. The host itself supplies input rather than interpreting these commands.
