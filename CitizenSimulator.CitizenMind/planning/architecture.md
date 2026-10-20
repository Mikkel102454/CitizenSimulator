# Brain architecture

## One decision loop, several time scales

At a simulation boundary, ingest completed world events, update beliefs, advance stress and memory decay, refresh due sensors, update commitments and projects, and check current execution. Then, only when necessary, score intentions, plan the selected intention, and start an action. Apply resulting world events at a defined subsequent boundary. Decisions in the same batch should read the same world revision; contention is resolved by domain services in deterministic order.

Suggested cadences, all measured in simulation time:

| Process | Initial cadence | Additional wake-up causes |
| --- | --- | --- |
| Executor and urgent safety | Every simulation tick | Death, cancellation, action result |
| Need and commitment evaluation | 5 minutes, staggered by citizen ID | Critical need, revised shift, travel failure |
| Nearby opportunities | 15 minutes when relevant | Arrival, shop closing, observed vacancy |
| Social and memory maintenance | 60 minutes | Significant interaction |
| Career review | Daily | Dismissal, missed pay, sustained severe stress |
| House/business projects | Weekly | Price, savings, credit, or premises change |

Use elapsed simulation intervals, never wall-clock timers or counts of rendered frames. Calendar boundaries crossed by a large tick must still be processed. At the current 300 ticks/day, one tick spans 4.8 simulated minutes, so finer decisions cannot honestly have sub-tick precision. Support split intervals or accept and document this quantization. Pausing freezes brain time.

## State ownership

Proposed ECS components can be grouped differently after profiling:

| State | Contents | Authority |
| --- | --- | --- |
| BrainProfile | Stable traits, values, preferences, seeded variation | Brain |
| BrainBeliefs | Typed fact values, source, confidence, observation and expiry times | Brain; may be wrong |
| BrainIntent | Current goal, target, score, commitment and reconsideration time | Brain |
| BrainExecution | Plan, action phase, operation ID, reservations, cancellation state | Brain plus executor |
| BrainSchedule | Preferred windows and referenced world commitments | Brain projection; contract is world owned |
| BrainProjects | Milestones, budget policies, obstacles and review time | Brain |
| BrainAffect | Stress, mood, short-lived emotions | Brain; physiological effects go through a domain service |
| BrainMemory | Bounded episodic records and semantic summaries | Brain |
| BrainRelationships | Directed affinity, trust, familiarity and perceived obligations | Brain; formal relationships are world owned |

Store entity IDs with lifetime/version validation, not persistent component object references. A dead citizen or destroyed business must not silently resolve to a reused entity. Keep ephemeral scratch planning state outside serialized components.

Needs use a normalized brain convention: hunger, thirst, fatigue, loneliness and stress are pressures in [0,1], where 1 is urgent. Domain adapters translate actual source units; do not assume today's Energy/Hunger fields already have this meaning. Monetary amounts are decimal values in a named currency, not normalized floats.

## Perception and knowledge

Sensors query the world and create observations. Perception policy controls discovery: a citizen can know their own contract and funds, public job listings they checked, nearby shops they visited, and information received socially. They do not automatically know every vacancy or everyone's feelings.

A belief contains `(key, subject, value, observedAt, expiresAt, confidence, source, sourceRevision)`. Unknown is a separate value; it is never silently converted to false or zero. Required unknown preconditions make an action unavailable until a sensing action supplies evidence. Stale beliefs can motivate a trip but cannot authorize a transaction. Prediction and observation remain distinct.

Repeated expensive queries should use domain-maintained indexes and cached, versioned snapshots. Candidate searches have stable ordering, paging and caps. Reaching a cap means “some candidates inspected,” not “no others exist.”

## Schedules and obligations

Sleep, meals and leisure are soft preferences. A job shift, accepted invitation, medical appointment or owned-business obligation is a dated commitment with location, start, end, arrival buffer, cancellation rules, importance and source revision. Never equate business opening hours with employee work hours.

Materialize recurring schedules into an absolute simulation timeline over a rolling horizon. A 23:00–07:00 window ends on the next date. Shifts also support crossing midnight and irregular rotations. Calculate `departAt = start - travelEstimate - arrivalBuffer`. Include preparation and food when estimating whether a plan fits before departure.

Sleep selection depends on fatigue, preferred window and time until the next commitment; it can include a nap. Wake time is the earlier of the preferred wake time or the preparation deadline for a commitment, unless critical exhaustion requires an override. Sleeping is a long-running action with periodic need updates and a wake checkpoint, not eight hours applied instantly.

On overlap, compare feasibility, severity and cancellation consequences; preserve contractual rules. Request leave, decline an invitation, shorten leisure, or record unavoidable absence. A critical need can interrupt work; payroll/attendance systems decide the consequence. Rebuild commitments after schedule revisions and invalidate affected plans.

## Utility chooses why; GOAP chooses how

Generate grounded intention candidates, such as satisfy hunger, attend the next shift, improve employment, recover from stress, meet a specific friend, or progress a house project. Each candidate has a feasibility gate and considerations. Do not encode one utility candidate per full scripted daily routine.

For the initial format, each consideration supplies a value in [0,1] through a clamped piecewise linear curve. Score an eligible candidate as `bias * sum(weight * value) / sum(weight)`, then clamp to [0,1]. Weights are nonnegative with a positive total. Biases are fixed per profile/definition; no random draw per evaluation. Trait-based values enter as named facts. Emergency arbitration occurs before normal scoring, so a weighted average cannot drown out a critical need.

Keep an incumbent for a minimum commitment period; switch only if a challenger exceeds its recomputed score by the configured margin. Emergency or infeasible incumbents bypass the hold. Do not switch through an executor's atomic transaction phase. Reconsider after completion, meaningful world changes, or a timed checkpoint, rather than discarding a plan each tick.

For the selected intention, GOAP searches a small, relevant, typed world projection. Ground actions using capped candidate providers: owned food, affordable meal offers, known vacancies, possible social partners. Actions have preconditions, simulated effects, estimated duration and nonnegative cost. Plans are chains of abstract actions, not every movement frame.

Initially use uniform-cost search (equivalently A* with h=0) to avoid an invalid heuristic. Track best known cost per canonical projected state; cap expansions, depth, candidates and duration. Include bound target IDs, quantities and relevant time in state identity. Cost combines elapsed minutes, spending scaled by discretionary budget, risk, effort, and conflict penalties. Hard feasibility checks forbid spending protected funds or missing mandatory deadlines unless an explicit override applies.

Planner effects only predict a possible future. They never change money, inventory or needs in the live world. Numeric effects must be bounded and action repetition limited; buying/cooking/eating cannot create free resources or exploit rounding. Time advances in projected state; quotes and shifts can expire during a plan.

If no plan exists or the budget runs out, keep a feasible incumbent or choose a bounded sensing/rest action and schedule a retry. Record the cause and back off only that goal/target pair. Reconsider earlier on relevant changes. Avoid recursive “plan to plan” chains or endless attempts to buy an unavailable item.

## Execution and transactions

Use a small action state machine: Pending → Validating → Running → Succeeded / Failed / Cancelled. Each action handler defines interruptible phases, checkpoints, resource reservations, deadlines and cleanup. Movement, waiting, work, sleep and cooking may run for many ticks; buying, signing and transferring ownership have atomic commit phases.

Before starting and committing, revalidate target existence, funds, stock, access, consent, contract revision and expiry. Domain services reserve/commit resources or return a structured failure. Assign idempotent operation IDs so a duplicate callback or reload cannot charge twice. Cancellation requests wait for a safe phase; release reservations and record any irreversible cost already incurred. Partial cooking may consume ingredients even if interrupted; the handler owns that rule.

Record success only from confirmed domain outcomes. On failure, update the relevant belief, emit a memory if significant, invalidate dependent steps and repair or replan. Another citizen purchasing the last meal is a normal conflict, not a reason to force success. Death cancels brain work and triggers reservation cleanup.

## Long-term projects

Projects retain intention across short-term interruptions. They store a chosen target class, milestones, estimates, budget policy, progress evidence, blocker reason and next review. Use a small milestone state machine or hierarchical task decomposition here; do not GOAP-search every meal for the next year.

House example: assess affordability → build a protected reserve → inspect listings → arrange financing if supported → make an offer → close → confirm ownership and occupancy. Amount needed derives from actual quotes, transaction costs and reserve requirements, not a fixed house price in YAML. Reassess when housing costs or family circumstances change. Savings are real account allocations if supported, otherwise a brain spending policy; they are never minted as a goal effect.

Business example: choose a feasible sector → learn required skills → estimate demand and runway → locate premises → obtain equipment and finance → register/open → operate and review. Opening a business adds ongoing commitments and financial risk; “owner” is not a permanent success state. Multiple citizens competing for the same premises must use the same transaction service.

Allow a small number of active projects. Compatible projects can coexist; house savings and startup capital compete for the same funds. Reserve essentials first, then allocate discretionary funds by priority without double counting. Changing priorities pauses or abandons a project with recorded reasons and actual sunk costs.

## Career decisions

Job search combines qualification, expected take-home income, travel, shift compatibility, stability, interest and remembered employer treatment. Application does not guarantee acceptance. Employers select candidates; offers expire and contracts bind both parties.

Review quitting separately from immediate stress relief. Consider stress duration, safety, missed pay, alternatives, notice terms, dependents and financial runway. Ordinarily secure an offer before resigning; urgent danger can justify quitting without one. Resignation is a domain request with effective date, not immediate deletion of employment. Continue attending required shifts until the contract ends or absence is authorized. A better offer may require negotiating start dates rather than holding conflicting commitments.

## Feelings and stress

Use lightweight appraisal, not a language model per citizen. Events affect goal progress, perceived control, social acceptance and safety. Those appraisals update joy, sadness, anger, fear and short-term arousal. Mood is a slowly changing baseline; stress is accumulated load with recovery. Traits change appraisal sensitivity and preferred coping activities.

Integrate stress over simulated elapsed time, for example `stress += (load * sensitivity - recovery) * elapsedHours`, clamped to [0,1]. Sustained workload, poor sleep, financial insecurity and conflict contribute load; sleep, leisure and supportive interaction contribute recovery when actually experienced. Cap overlapping contributions and use persistence thresholds so one bad tick does not cause a resignation. Stress can increase fatigue and error risk only through explicit domain rules, and should not multiply its own input indefinitely.

Emotion modifies selected utility considerations, memory salience and social responses within bounded ranges. A feeling never bypasses affordability, consent or transaction rules. Show the factors in inspection tools so behavior can be explained and tuned.

## Relationships and memory

Relationships are directed: A trusting B does not imply B trusts A. Store sparse edges for known citizens with affinity [-1,1], trust [0,1], familiarity [0,1], last contact, shared commitments and tags derived from history. Friendship/romance/family are distinct concepts; formal partnerships and household membership need authoritative records. Mutual consent and eligibility must be checked for invitations and romantic actions. Rejection may change feelings; it never forces acceptance.

Interaction proposals are bilateral transactions: invitation → recipient decision using their own utility and schedule → accepted dated commitment or declined result. Reserve participants/slots to prevent duplicate meetings. Conversation, help, conflict and broken promises emit outcomes from which each participant forms their own appraisal. Partner selection should consider accessibility and recent contact, not repeatedly pick only the highest-scoring friend.

Keep bounded episodic memory: event kind, participants, time, perceived outcome, confidence and salience. Consolidate repeated events into semantic summaries such as “this employer often pays late”; retain evidence counts and recency. Decay weak records; preserve summaries of major events. Start with at most 128 episodes and 64 active relationship edges per citizen, with eviction policies that protect active commitments and close ties. Formal family records and contracts live elsewhere and are not forgotten when an edge is evicted.

Learning adjusts estimates and preferences within configured bounds: expected travel time, meal satisfaction, employer reliability or trust. An exponential moving average can update an estimate toward observed outcomes. Avoid learning from predicted effects or counting repeated delivery of the same event. No arbitrary YAML rewriting is needed for learning.

## Reproducibility, scale and inspection

Use seeded random streams keyed by citizen and decision category; persist their state. Apply stable candidate ordering and tie breaks by ID. Budget work in deterministic operation counts, not wall time. Deterministic world command ordering resolves competition for scarce resources.

Stagger sensors and reviews, share read-only public listing indexes, cache valid plans by relevant fact revisions, and wake citizens only for subscribed changes. Run physiological updates and deadlines for all citizens even if distant; approximate off-screen interactions only when they obey the same resource rules. Profile before choosing population targets.

Persist profile, beliefs with timestamps, schedules, project progress, affect, memory, relationships and executor state, plus schema/content versions and random streams. On load, reconcile in-flight operation IDs and reservations with domain services; do not replay an unconfirmed purchase blindly. Migrate known older formats, reject unknown incompatible versions, and invalidate plans referencing removed definitions.

Provide a citizen inspector with current goal, competing scores and gates, commitment timeline, known versus actual facts, current plan and action phase, project blockers, stress inputs, memories and relationship changes. Keep bounded traces for “why did they quit?”, “why did they miss work?” and “why buy takeaway?”.
