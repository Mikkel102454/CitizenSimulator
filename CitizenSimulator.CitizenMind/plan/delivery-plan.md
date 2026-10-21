# Delivery and validation plan

Implement later in small vertical slices. This planning task makes no runtime changes. Every phase requires an inspector trace and deterministic acceptance scenarios before expanding scope.

## 1. Contracts, time, needs and loader

Agree on need scales/initial values, currency and game time mapping with domain owners. Add typed capability/fact registries, versioned YAML loading, pure expression evaluation, profile initialization and bounded decision tracing. Use fake world adapters for unavailable domains. Initialize citizens both at startup and creation; handle death.

Acceptance: malformed/unknown keys fail clearly; duplicate IDs fail; missing optional domains disable only dependent features; same seed and input produce the same profiles and scores; a paused simulation changes nothing. Calendar tests include midnight, overnight shifts and a tick crossing several deadlines. Needs initialization must permit survival and correct eating/drinking effects before moving on.

## 2. Survive one day

Implement hunger/thirst/fatigue sensing, utility arbitration, bounded GOAP, sleeping/waking, owned-food consumption, drinking and safe waiting. Add execution state machine, cancellation, idempotent commands and real outcome refresh. At this phase, use a deliberately small action library and inspect every failure path.

Acceptance: sleep usually follows 23:00–07:00; extreme fatigue can cause a nap; hunger interrupts discretionary leisure; thirst has a real response; unavailable food cannot be consumed; unknown facts do not become zero. A citizen can pursue other activities after a plan fails without spinning every tick. Action phase and interruption reason are visible.

## 3. Food economy and travel

Add known shop discovery, quotes, travel and transactions; implement buy-ready-meal and ingredients/cooking paths. Compare those paths by budget, time, fatigue and preference.

Acceptance: a rested citizen with ingredients cooks; a late citizen who can afford it buys a quick meal; an exhausted citizen avoids a lengthy recipe; an unaffordable meal is rejected before commit. Two citizens competing for the last meal cannot both receive it; stock and funds stay conserved. An expired quote or cancelled trip does not teleport food into inventory. No feasible food option produces a visible welfare problem, not invented resources.

## 4. Employment and commitments

With a real Employment domain, add vacancy search, pending applications, offer comparison, contract acceptance, shift fetching, travel/check-in, breaks, payroll and resignation. Include skills and knowledge restrictions.

Acceptance: a 09:00 shift with 25-minute commute and 10-minute buffer sets departure to 08:25; opening hours never replace a contract's shifts. A changed shift updates the plan; night shifts cross dates correctly. Rejected applications leave citizens unemployed. One opening cannot yield two accepted contracts. Notice terms preserve attendance until the effective end. Stress may trigger a job search before resignation; critical safety can override the usual offer-first preference.

## 5. Affect, memory and social life

Add event appraisal, bounded stress integration/recovery, sparse directed relationships, bilateral invitations, episodic memories, consolidation and bounded preference learning.

Acceptance: repeated missed pay affects that employer's reliability estimate; a single minor frustration does not trigger quitting; rest recovers stress over elapsed time; duplicate events do not double-count emotion. A friend can decline due to a shift; both parties reserve a meeting only after agreement. A likes B does not imply B likes A. Memory/relationship eviction preserves active commitments and formal family records.

## 6. Persistent projects

Add house and enterprise milestones only after world domains support paid transactions, ownership/access and operational checks. Budget arbitration preserves essentials and prevents multiple projects from reserving the same money.

Acceptance: house savings continue after a meal/work interruption; a price rise revises affordability; loss of income pauses a project. Competing buyers cannot both own a property. Startup creation cannot bypass equipment/premises/funding. Business ownership creates ongoing work/risk instead of completing all future livelihood goals.

## 7. Save/load, tuning and scale

Validate saves during travel, sleeping, an unresolved application, a committed purchase awaiting event delivery and a social reservation. Reconcile operations by ID without duplicating effects. Test pack version migration/removal and replay consistency. Implement hot reload only after lifecycle reconciliation is reliable.

Measure at several population sizes, such as 100 / 1,000 / 10,000, without promising a target before profiling. Record planning expansions, sensor requests, tick cost, queue lateness, memory per citizen and starvation of lower-priority goals. Make work budgets deterministic; deadlines and physiology must continue under planner load.

Use seeded multi-day simulations to check distributions rather than force identical lives: sleep duration, missed shifts, hunger crises, unemployment duration, turnover, debt, stress, social isolation, house ownership and enterprise survival. Distinct traits and circumstances should produce different plausible choices, while every transaction remains valid.

## Useful first scenario fixtures

| Setup | Expected reasoned outcome |
| --- | --- |
| 22:55, rested enough, no urgent needs | Prefer sleep near the configured window |
| 07:30, hungry, shift departure 08:25 | Select a meal plan that fits including travel/preparation |
| No job, public suitable vacancy | Apply; wait for the employer's actual decision |
| High stress for several days, limited savings | Seek relief/another job, assess runway before quitting |
| Dangerous workplace, no offer | Permit a safety-driven resignation despite financial cost |
| Friend invites citizen during work | Decline or propose another time; record no accepted overlap |
| House and startup both want the same funds | Allocate once according to project priority |
| No shop, no food, no money | Record unmet need and seek supported help; do not fake a purchase |

These are validation cases, not scripts to encode in the brain. Include counterexamples and randomized resource contention so tests verify the rules rather than mirror a particular implementation.
