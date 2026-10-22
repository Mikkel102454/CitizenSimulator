# CitizenMind schedules

Schedules ask a citizen to satisfy a registered goal within a game-time window. They do not implement work, travel, sleeping, or other activities. Supply goals, actions, facts, and handlers for those activities first.

All schedule calls use `ICitizenMindService` and require a live entity with `MindComponent`. The examples assume `mind` is that service, `citizen` is the entity, and `example.attend_work` is a goal already registered by your mod.

```csharp
using CitizenSimulator.CitizenMind.Planning;
using CitizenSimulator.CitizenMind.Scheduling;
```

## One-off commitments

`SetSchedule(EntityRef citizen, IReadOnlyList<ScheduledCommitment> commitments)` replaces the citizen's manual commitments. Times are absolute game time since the calendar's game-time origin.

```csharp
mind.SetSchedule(citizen, new[]
{
    new ScheduledCommitment
    {
        Id = "example.work_visit",
        GoalId = "example.attend_work",
        Window = new TimeWindow(TimeSpan.FromHours(9), TimeSpan.FromHours(10)),
        Priority = 10,
        PreparationTime = TimeSpan.FromMinutes(30)
    }
});
```

This window is on day zero. Add `TimeSpan.FromDays(dayIndex)` to both timestamps for a later day, or calculate a window from current calendar game time. Start must be nonnegative, end must be later than start, and preparation time must be nonnegative. IDs must be unique in the supplied list; the prefix `citizen_mind.daily:` is reserved.

Preparation permits this commitment to displace ordinary activity before the window opens. It does not automatically move the citizen or reserve preparation time. Include travel and preparation as actions and allow enough lead time.

Replacing manual commitments preserves generated daily occurrences and other mod sources. An unchanged ID, goal ID, start/end window and target retains its previous outcome, even when priority or preparation time changes. Use a new occurrence ID for a genuinely new appointment.

## Independent mod sources and occurrence targets

`SetScheduleSource(citizen, sourceId, commitments)` replaces only one mod's commitments. Use a stable nonblank source such as `business.employment`. Manual calls, daily rules and other sources remain intact. An unchanged source update is a no-op; IDs must be unique across every source. Invalid updates leave the schedule unchanged. Supply an empty list to withdraw that source's commitments.

By default, an occurrence uses its registered goal's desired conditions. An optional `Target = new CommitmentTarget(factId, minimum)` replaces those conditions for that occurrence with `fact >= minimum`. The fact must be a registered `double` and the minimum must be finite. Eligibility and actions still come from the registered goal. Targets apply consistently during selection, forecasts, live validation and completion. Changing a target creates a new pending outcome rather than reusing an old completion.

Business uses this for real employment shifts: each commitment targets `employment.completedShiftEndMinutes` at its exact shift-end timestamp. Clock time alone cannot satisfy it; real attendance through the end updates the domain fact. Today's completion cannot satisfy a later occurrence. Partial attendance through the end can complete the commitment, while payroll pays only actual attended minutes.

`EmploymentScheduleSystem` publishes eight days of confirmed shifts before minds run, with priority 40 and commute plus one tick of preparation. Overnight shifts keep full start/end times. Dismissal and suspension withdraw current/future shifts, and seven days of outcomes remain available. Safety emergencies may interrupt work and check the worker out. This source does not replace manually configured or recurring schedules.

## Recurring daily rules

`SetDailySchedule(EntityRef citizen, IReadOnlyList<DailyScheduleRule> rules)` replaces recurring rules without replacing manual commitments.

```csharp
mind.SetDailySchedule(citizen, new[]
{
    new DailyScheduleRule
    {
        Id = "example.weekday_work",
        GoalId = "example.attend_work",
        Window = new DailyTimeWindow(TimeSpan.FromHours(9), TimeSpan.FromHours(10)),
        Priority = 10,
        PreparationTime = TimeSpan.FromMinutes(30),
        Weekdays = new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday
        }
    }
});
```

`DailyTimeWindow` uses offsets from midnight. Start must be at least zero and less than 24 hours. End may be zero through 24 hours but cannot equal start. An end earlier than start crosses midnight, so `22:00` to `06:00` is an overnight window. Its weekday is the start day's weekday according to `ICalenderService`.

`Weekdays = null` means every day; an empty weekday list disables the rule. Rule IDs must be unique within the supplied list. Identical rule sets are a no-op. Changed or removed rules discard their pending occurrences; unchanged rules retain pending occurrences. Completed and missed occurrence history remains, including when a rule ID is reused.

The next mind update generates occurrences through the planning horizon, one game day by default. Generation also considers the preceding day so an active overnight window can be included. It does not backfill all past days.

Daily recurrence does not reset world facts. If `example.attend_work` remains satisfied from yesterday, a new occurrence can complete as soon as its window opens. Model date-specific attendance or reset the relevant state in the owning mod.

## Selection and outcomes

Emergencies take precedence over commitments. Ready commitments take precedence over ordinary utility choices, ordered by descending priority, earliest deadline, then ID. A goal must be eligible or already satisfied to be selected for a commitment; a schedule does not override an ineligible goal.

`GetSchedule(citizen)` returns manual, mod-supplied and generated `ScheduledCommitmentState` entries. Each exposes `Commitment`, `Status`, `SourceId`, `SourceRuleId` and `OccurrenceDay`. `SourceId` identifies a publishing mod; rule/day identify generated daily occurrences. These are null for manual entries. Status is managed by CitizenMind.

| Status | Meaning |
| --- | --- |
| `PENDING` | Awaiting satisfaction within the window. |
| `COMPLETED` | Desired conditions were observed satisfied inside the window. |
| `MISSED` | The window ended without recorded completion. |

A desired state reached before the window opens does not immediately complete the commitment. After the end, pending entries become missed. Failed searches and execution can retry while time remains; scheduling is a request with feasibility checks, not a guarantee.

Schedule revisions invalidate affected planning work and trigger fresh checks before new actions start. A running non-interruptible action can delay the change until it finishes.

## Read and clear schedules

`GetDailySchedule(citizen)` returns the current recurring rules. The returned schedule and rule collections are read-only views; use service calls to change them.

`ClearDailySchedule(citizen)` removes daily rules and pending generated occurrences, preserving manual commitments and generated outcomes. `SetSchedule(citizen, Array.Empty<ScheduledCommitment>())` removes only manual commitments. `ClearSchedule(citizen)` removes everything: manual commitments, daily rules, and occurrence history.

There is no built-in schedule YAML format. If your mod exposes schedules in its own files, deserialize your own configuration types and translate them into these API calls.
