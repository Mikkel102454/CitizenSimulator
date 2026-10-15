# Simulation clock

Run the host with `dotnet run --project CitizenSimulator`.
Optional startup settings: `-- --tick-rate 30 --batch-size 8`.

Console commands (numbers use invariant culture):
- `pause` / `resume`
- `speed 2` (finite positive multiplier)
- `step` or `step 10` (while paused)
- `advance 60` (advance by simulation seconds while paused)
- `until 3600` (advance to absolute elapsed simulation seconds while paused)
- `status`, `help`, `quit`

Stepping and advancement execute unpaced, bounded batches and finish paused.
Use `pause` to cancel an active advance. Targets round up to a tick boundary.
Manual advances clear pending real-time backlog. Ordinary pause/resume preserves it.
Overload retains every pending tick and reports backlog rather than skipping updates.
An input EOF leaves the simulation running; use `quit` or Ctrl+C to exit.
Automatic status output is enabled only with interactive input.

Systems implement `OnUpdate(SimulationTime time, ICommandBuffer commandBuffer)`.
Time describes the tick being executed, starting at tick 1. Only a successful
scheduler update commits that tick to the shared clock. Structural mutations
continue to use the command buffer. The mod context exposes read-only clock state;
use the supplied update time for current-tick calculations.

`Delta` is the fixed 1/tick-rate duration represented as a TimeSpan, rounded to
100ns precision. Absolute elapsed time derives from the integer tick count to
avoid accumulated rounding drift. No timers, calendar, snapshots, or per-system
update frequencies are included.

Controls and clock mutations belong on the simulation thread between batches.
Real elapsed time is fed before applying controls, so paused wall time cannot
become resume backlog. Failed updates fault the clock and trigger host cleanup;
partial world changes are not rolled back or retried.

Diagnostics sample throughput and average/max execution cost over real-time
windows of at least one second. Tick cost includes scheduler callbacks and
command playback. Backlog is immediate; overload is flagged once pending ticks
reach a full batch. At high speeds the achieved rate can be below the requested
rate because CPU capacity limits throughput.

