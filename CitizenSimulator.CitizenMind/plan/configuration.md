# Proposed configuration contract

The YAML files in `examples/` form one pack. They are illustrative input to a future loader, not supported settings today. Use structured expressions rather than arbitrary scripts or embedded C#.

## Loading and validation

Each file declares `schema_version: 1` and the same `pack: citizen_brain.core`. A future loader reads declared files in the pack manifest, parses YAML safely (no custom object tags), rejects duplicate mapping keys, resolves namespaced IDs and validates the full pack before activation. Unknown fields, missing references, invalid units, cycles in profile inheritance, and incompatible capability versions are errors with file/line context. Limit file size, nesting, collection sizes and aliases.

Definitions use explicit IDs. No silent last-file-wins merging: another pack uses an explicit `extends` or `patch` operation, constrained by the target schema. Profile inheritance uses one parent; maps merge by key, scalars replace, lists replace unless a schema explicitly declares keyed merging. Freeze the resolved pack for a decision epoch. Reload atomically after validation, retain the old pack if validation fails, and finish or safely cancel actions whose definitions changed. Save games reference pack versions and content hashes.

## Types and expressions

Facts, query results and bindings are registered typed keys. Minimum types: bool, integer, decimal money with currency, normalized number, entity ID, instant, duration, enum and bounded set. Unit mismatch is an error. All facts and provider outputs used by these examples are listed below; they are proposed adapter outputs.

Expression forms in the examples:

| Form | Meaning |
| --- | --- |
| `{fact: key}` | Read a known snapshot fact |
| `{binding: target.field}` | Read a grounded candidate field |
| `{literal: value}` | Typed literal; expected type comes from context |
| `{op: gte, args: [a, b]}` | Comparison; also `lte`, `eq` |
| `{op: and, args: [...]}` | Boolean conjunction; also `or`, unary `not` |
| `{op: add, args: [a, b]}` | Arithmetic in matching units; also `subtract` |

Expression evaluation is pure, bounded and deterministic; missing data propagates Unknown. Eligibility and preconditions require True. Denominators, overflows, infinities and negative durations are rejected. The DSL may expand later, but content cannot call arbitrary reflection, filesystem or network operations.

For utility inputs only, bool converts explicitly to false=0 / true=1. Bare parameter strings such as `profile.schedule.preferred_sleep` and `schedule.next_shift_id` are registered context references in the handler parameter schema, not general expressions. The latter is a stable shift ID supplied by the schedule adapter. Prefer converting these to fully typed reference nodes when implementing the final schema.

Goal `desired` entries are conjunctive predicate expressions over the planner projection. `eligible` is checked before scoring. `emergency` is an optional boolean gate and integer priority; higher priority wins before normal utility. Utility considerations have input expressions, nonnegative weights and clamped piecewise linear curves `[[input, output], ...]` with strictly increasing x values. Inputs and outputs are finite; outputs are [0,1]. Scores use the architecture's weighted-mean formula.

Planner `prediction` effects support `set`, `add` and `subtract` on registered writable projection facts. A step cannot write authoritative world state. Handler outcomes refresh the projection from real observations before continuing. Bindings select candidates once per planning epoch; all fields, parameters and output mappings are type checked against registered handler/provider descriptors. Each grounded action has a stable ID built from definition ID and bound entity/item IDs.

World query snapshots are fixed for a planning epoch, but resource grounding also runs over each projected successor state. `projected_outputs` creates plan-local resource tokens with provenance and semantic payload (`ready_meal` or `ingredient_portion`) supplied by the bound offer/recipe descriptor. This is necessary to plan buy → cook → eat before those objects exist. Resource providers return both observed and plan-local candidates; a cookable recipe must be compatible with the specific ingredients. The numeric count effects are coarse filters, not substitutes for these tokens. Consuming a portion removes its token and quantity exactly once. State hashing includes token kind, recipe compatibility, nutritional payload and provenance.

At execution, resolve a plan-local output token to the actual returned inventory item/portion from the preceding command. If the resource or payload differs, invalidate and replan; never send a fictional token ID to a world service. No-output descriptors cannot claim new resources just by incrementing a count. Food handlers include any required trip to the accessible eating/cooking location in `total_minutes`; richer location state can replace these compound handlers later.

Durations and costs are separate: duration advances projected time; cost is a nonnegative dimensionless number combining configured features. `duration` may be fixed or a typed candidate estimate; neither is a promise of completion. `requires_fit_before_commitment: true` rejects plans whose projected end plus travel buffer misses the next protected obligation. It does not prevent the attend-shift plan from targeting that obligation. The executor enforces actual deadlines too.

## Facts used by the example pack

| Fact | Type / meaning | Producer |
| --- | --- | --- |
| `needs.hunger`, `needs.thirst`, `needs.fatigue` | [0,1] pressure | Needs adapter |
| `affect.stress` | [0,1] accumulated stress | Affect system |
| `schedule.sleep_preference_active` | bool, current local date/window | Schedule |
| `schedule.minutes_until_departure` | Signed minutes; large sentinel is forbidden, use a separate known gate | Schedule |
| `schedule.has_next_shift`, `schedule.at_workplace`, `schedule.attending_shift` | bool | Schedule/travel/employment |
| `career.search_desire` | [0,1], compiled profile and career appraisal | Career review |
| `career.application_pending` | bool | Employment adapter |
| `career.quit_ready`, `career.review_due` | bool | Career review; readiness includes notice/runway/safety policy |
| `employment.has_job` | bool from confirmed contracts | Employment adapter |
| `inventory.ready_meal_count`, `inventory.ingredient_portions` | Nonnegative integer abstract portions | Inventory/recipe adapter |
| `home.can_cook`, `home.can_sleep` | bool, known access and equipment | Home adapter |
| `home.can_drink` | bool, known accessible safe water source | Home/water adapter |
| `finance.discretionary` | Money in pack currency after essentials/reserves | Finance adapter/policy |
| `social.contact_pressure`, `social.interaction_completed` | [0,1] pressure; bool for current intention epoch | Social system |
| `projects.house.affordable`, `projects.house.owned` | bool, fresh estimate / actual title evidence | Housing/project adapter |
| `projects.business.feasible`, `projects.business.operating` | bool, verified funding/access / world operation | Enterprise/project adapter |
| `projects.review_completed` | bool for current intention epoch | Project system |
| `career.review_completed` | bool for current intention epoch | Career system |

Transient completion facts reset when a new matching intention is created; retained world facts do not. Derived facts must declare their input revisions so a changed contract or budget invalidates a cached result.

## Grounding provider outputs

`food.meal_offers` returns target `offer` with `id`, `price` (money), `travel_minutes`, `total_minutes`, `hunger_after` and `expires_at`. `food.ingredient_offers` returns the same offer price/time fields for one cookable portion. `food.cookable_recipes` returns `recipe` with `id`, `minutes`, `hunger_after`, and required ingredient/equipment bindings; projection counts in this starter pack assume a single normalized recipe portion. A real catalog adapter must prevent treating incompatible ingredients as interchangeable. `food.owned_meals` returns a bound `meal` with ID and `hunger_after`; the count fact is a coarse gate, and the handler verifies the bound portion exists.

`employment.known_vacancies` returns `vacancy.id`, `revision`, `application_minutes`. `social.available_contacts` returns `contact.id`, `location_id`, `estimated_minutes`; availability means a proposal is plausible, not accepted. All providers apply knowledge policy, stable ordering, eligibility filters and manifest caps.

All handlers listed in `actions.yml` need descriptors and implementation. Goal action allowlists intentionally restrict GOAP's branching. The examples demonstrate a small vertical slice; meals at work, advanced contracts, romance and obtaining water when home access is absent need additional definitions and handlers. The delivery plan does not consider survival complete without thirst handling.

## Project and appraisal interpreters

`projects.yml` uses an ordered milestone interpreter. A milestone becomes complete only when its `evidence` expression is true. `request` names a registered project handler, retried only at its review cadence or after relevant events; handlers may yield a bounded intention, ask for sensing, or wait for an external result. They do not directly grant ownership/funds. Project budget policies apply before exposing discretionary money. Intention generation from requests is a future handler contract, not implicit GOAP magic.

`social-memory.yml` defines event appraisal rules. Event filters are registered event IDs, whose payloads include actor/recipient IDs, occurredAt, unique event ID and outcome. Deltas apply only to the observing citizen's affect or directed relationship. Rate limits are per observer/subject/rule in simulation time. Clamp state to declared ranges, deduplicate events before updates, and never apply a prediction as a remembered experience.
