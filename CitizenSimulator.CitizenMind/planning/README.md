# CitizenBrain planning

This folder is a design proposal, not an implemented brain or executable configuration. All YAML uses a proposed version 1 format. No runtime files or other mods are changed. Repository observations describe the code inspected on 2026-10-19; adapters should absorb future API changes.

Start with [architecture.md](architecture.md), then [integrations.md](integrations.md), [configuration.md](configuration.md), and [delivery-plan.md](delivery-plan.md). The [examples](examples) directory contains a single fictional behavior pack, split into files that are loaded together. Its IDs, facts, queries, expressions, handlers, and events require implementation before use.

## Recommended approach

Use a hybrid with clear responsibilities:

| Part | Responsibility | Example |
| --- | --- | --- |
| Needs and world simulation | Produce actual conditions | Hunger rises; a shop sells its last meal |
| Perception and beliefs | Represent what this citizen knows | Yesterday's vacancy might still exist |
| Schedule and commitments | Track preferences and obligations | Prefer sleep 23:00–07:00; shift starts 09:00 |
| Utility AI | Choose the current intention | Eat now, keep working, or find a better job |
| Bounded GOAP | Find steps for that intention | Buy ingredients → cook → eat |
| Action executor | Run and verify real actions | Request a purchase and handle its rejection |
| Projects | Maintain longer ambitions | Save for a house over several months |
| Memory, emotions, relationships | Shape future choices | A broken promise reduces trust |

The important boundary: YAML chooses and combines capabilities; code provides capabilities and enforces the world's rules. YAML cannot make travel, cooking, contracts, or banking exist by itself. Once generic capabilities exist, new combinations should usually need data rather than new scenario code.

“Evolve on its own” means emergent behavior from changing circumstances, learned preferences, persistent consequences, and interactions. It does not mean rewriting its own rules or inventing unsupported actions. An unemployed cook can discover a vacancy, accept a job, become stressed, look for another job, and eventually start a business without a script for that life story. Each step still obeys the same economic and social rules as everyone else.

## Initial assumptions

- Citizens are autonomous individuals with bounded knowledge, resources, and decision budgets.
- Preferred sleep is 23:00–07:00 by default, customized per citizen. Employment can require a different routine.
- Survival has priority over ordinary commitments, but preferences and stress do not automatically excuse absence or void contracts.
- Citizens can make imperfect decisions. Use limited information and imperfect estimates, not arbitrary failures after valid transactions.
- The authoritative world owns money, property, stock, employment, health, and legal relationships. The brain owns intentions and beliefs about those things.
- Deterministic simulation and readable decision explanations are first-class requirements.
- Initial values and timing thresholds in this proposal are tuning starting points, not claims about human psychology.

## Decisions that can wait

Population size and desired simulation speed will determine planning budgets and social detail. The plans default to bounded search and sparse relationships so they can be measured before choosing a final target. Travel needs an adapter whether it becomes a detailed map or an abstract duration model. Business founding and house purchases remain disabled until their world services can perform complete transactions.
