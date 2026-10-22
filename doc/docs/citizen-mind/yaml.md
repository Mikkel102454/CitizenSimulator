# Goals and actions in YAML

## Loading definitions

The current format is one `GoalDefinition` or `ActionDefinition` per YAML document, with camelCase property names. CitizenMind loads only its own embedded resources whose names contain `.Data.Goals.` or `.Data.Actions.` and end in `.yml`. Files ending in `.yml.disabled` are excluded.

Placing a loose YAML file in `Mods` does not register it. Another mod must load its own resources or files, deserialize them, and call the registration API during `OnLoad`. For example, with YamlDotNet referenced and `mind` retrieved from the service registry:

```csharp
using CitizenSimulator.CitizenMind.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

var yaml = new DeserializerBuilder()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)
    .Build();

mind.RegisterGoal(yaml.Deserialize<GoalDefinition>(goalText));
mind.RegisterAction(yaml.Deserialize<ActionDefinition>(actionText));
```

`goalText` and `actionText` are strings read by your mod. There is no current pack, profile, or schedule YAML loader. Runtime schedules use the [schedule API](schedules.md).

## Goal example

This example uses a predictable `double` fact named `example.fatigue`. Your mod must register that fact and the action's handler. It describes a short rest that lowers fatigue; the handler must apply that change in the real world.

```yaml
id: example.rest
eligible:
  op: gte
  args: [{fact: example.fatigue}, {literal: 0.4}]
emergency:
  when: {op: gte, args: [{fact: example.fatigue}, {literal: 0.95}]}
  priority: 100
desired:
  - {op: lte, args: [{fact: example.fatigue}, {literal: 0.2}]}
utility:
  bias: 1.0
  considerations:
    - input: {fact: example.fatigue}
      weight: 1
      curve: [[0, 0], [0.4, 0.2], [1, 1]]
goalActions: [example.take_rest]
```

`id`, `eligible`, `desired`, `utility`, and `goalActions` are required. `emergency` is optional. `desired` and `goalActions` must each contain at least one entry. `goalActions` lists action IDs to seed dependency resolution, not an ordered script.

`utility.bias` defaults to `1`; each consideration's `weight` defaults to `1`. Bias and weights must be finite and nonnegative, with positive total weight. Curves need at least one `[input, score]` pair, strictly increasing finite inputs, and finite scores in `[0, 1]`.

## Action example

```yaml
id: example.take_rest
handler: example.rest_handler
parameters:
  targetFatigue: {type: DOUBLE, value: "0.2"}
preconditions:
  - {op: gt, args: [{fact: example.fatigue}, {parameter: targetFatigue}]}
duration:
  unit: simulationMinutes
  estimate: {literal: 30}
prediction:
  - fact: example.fatigue
    op: set
    value: {parameter: targetFatigue}
completion:
  - {op: lte, args: [{fact: example.fatigue}, {parameter: targetFatigue}]}
cost:
  expression: {calculated: durationMinutes}
execution:
  interruptible: true
  timeoutMinutes: 45
```

`id`, `handler`, `duration`, `prediction`, `completion`, and `cost` are required. `parameters` and `preconditions` default to empty collections. `execution` defaults to interruptible with no timeout. `candidateProvider` is an optional registered provider ID; use it when an action needs choices such as a shop or destination.

Preconditions and completion are lists whose entries must all pass. Keep prediction and completion consistent with what the handler really does. Empty completion lists provide no live verification.

Duration units are exactly `simulationMinutes`, `simulationHours`, and `simulationDays`. Estimated duration and search cost must be finite and nonnegative. Cost is a dimensionless planning score; lower cost makes a plan cheaper to the search. An optional timeout must be finite and strictly positive and is always in game minutes.

## Parameters

Each parameter has `type` and a string `value`. Quote values and use invariant numeric formatting, such as `"0.25"`.

| Type | C# value | Example |
| --- | --- | --- |
| `BOOLEAN` | `bool` | `"true"` |
| `INTEGER` | `int` | `"3"` |
| `DOUBLE` | `double` | `"0.25"` |
| `DECIMAL` | `decimal` | `"12.50"` |

String and entity parameters are not supported by this parameter schema. Use candidate bindings for a selected entity or other provider-specific data. Do not include `resolvedParameters` in YAML; registration builds it from `parameters`.

## Expressions

An expression must specify exactly one of the following forms. Nested operator arguments are expressions too.

| Form | Meaning | Allowed location |
| --- | --- | --- |
| `{fact: example.fatigue}` | Current observed or projected fact | Goals and actions |
| `{literal: 0.2}` | Constant nonnull value | Goals and actions |
| `{op: subtract, args: [...]}` | Operation on two arguments | Goals and actions |
| `{time: totalMinutes}` | Absolute game minutes | Goals and actions |
| `{time: minuteOfDay}` | Game minutes modulo 1440 | Goals and actions |
| `{parameter: targetFatigue}` | Parsed action parameter | Actions |
| `{binding: price}` | Declared candidate binding | Actions with a candidate provider |
| `{before: example.fatigue}` | Fact captured before the action | Action completion |
| `{calculated: durationMinutes}` | Evaluated action duration in minutes | Action cost, prediction values, and completion |

Supported operators are `add`, `subtract`, `multiply`, `divide`, `min`, `max`, `gt`, `gte`, `lt`, and `lte`. Every operator takes exactly two arguments. There are no current `and`, `or`, `not`, or equality operators. Use separate condition list entries for conjunction where a list is available. Boolean conditions must resolve to a boolean; a numeric fact alone is not a condition.

For example, verify an increase relative to the starting observation:

```yaml
completion:
  - op: gte
    args:
      - {fact: example.progress}
      - op: add
        args: [{before: example.progress}, {literal: 1}]
```

## Prediction effects

Each prediction entry names a registered predictable `fact`, an `op`, and a value expression. Supported operations are `set`, `add`, and `subtract`. An action may target a fact only once. Arithmetic effects read the previous target value; `set` replaces it.

Prediction operates only on the planner's state. Registering a fact with `canPredict: true` and declaring an effect does not update live components. A handler reporting success without the promised world changes will fail completion verification.

## Definition failures

Startup validation rejects unknown IDs, unsupported expression forms, incorrect argument counts, invalid curves, read-only prediction targets, duplicate effect targets, and unsupported duration units. Parameter parsing happens during action registration. Some value and type errors require real observations and appear during evaluation, so use matching fact, parameter, and binding types and guard division against zero.

The built-in thirst goal is `citizen_brain.core.satisfy_thirst`, and its action is `citizen_brain.core.drink_safe_water`. Those IDs retain the `citizen_brain` prefix even though the mod ID is `citizen_mind`. Their supporting facts, predictor, and drinking handler are supplied by the Needs integration.
