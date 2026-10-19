# Build a mod

## Create the project

Use the .NET 10 SDK. Create a class library beside the existing projects, for example `ExampleMod/ExampleMod.csproj`. Reference the same abstraction project that the host uses.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../CitizenSimulator.Abstractions/CitizenSimulator.Abstractions.csproj" />
  </ItemGroup>
</Project>
```

Reference a shared contract assembly when your mod uses types defined outside the core abstractions. Declare the providing mod ID when it must initialize first. A project reference makes types available to the compiler. A mod dependency controls runtime initialization order.

## Define the entry point

Create a public concrete class called `ExampleMod` implementing `CitizenSimulator.Abstractions.Modding.IMod`. Give it a public constructor with no arguments, or use the implicit constructor. Apply this attribute to the class.

```text
[Mod("example", "Example mod", "1.0.0")]
```

Implement `void OnLoad(IModContext context)`. The loader calls it once for this load attempt after required dependencies have initialized. Keep constructors free of world setup because discovery constructs mod instances before initialization order has been resolved.

If you create a separate mod that provides a shared service, declare its ID as a dependency. For a provider you name example_services, metadata looks like this.

```text
[Mod("example", "Example mod", "1.0.0", Dependencies = new[] { "example_services" })]
```

The displayed version is descriptive metadata. The loader does not enforce version ranges.

## Define component data

Create a public class `CounterComponent` deriving from `Component<CounterComponent>`. Add an integer property called `Count` with public accessors. The base class supplies `Parent` and `GetComponentType()`.

Component types in valid discovered mod assemblies are registered automatically before any mod receives `OnLoad`. For component classes in a separate helper assembly without a discovered mod entry point, explicitly register the type through `context.Components.Register<CounterComponent>()`.

## Register behavior

Create a public class `CounterSystem` deriving from `GameSystem`. Its constructor accepts an `IEventBus` and stores it for later publication. Give the base constructor priority `10` if this system should run after systems with priority `0`.

Inside `OnLoad`, perform this pseudocode sequence.

```text
context.Systems.Register(new CounterSystem(context.Events))
entity = context.Store.CreateEntity()
context.Store.AddComponent(entity, new CounterComponent { Count = 0 })
```

The entity and component are immediately visible because loading happens before scheduled execution. Systems are registered explicitly. Discovering a mod does not automatically register its systems.

## Implement callbacks

Override `OnStart(ICommandBuffer commands)` and cache the component query.

```text
counterQuery = CounterComponent.GetComponentType()
```

Override `OnUpdate(SimulationTime time, ICommandBuffer commands)` with this pseudocode.

```text
for each entity in Store.Query(counterQuery)
    counter = Store.GetComponent<CounterComponent>(entity)
    if counter is null
        continue
    counter.Count = counter.Count + 1
    if counter.Count >= 30
        events.Publish(new CounterCompleted(entity, counter.Count))
        commands.RemoveComponent<CounterComponent>(entity)
```

Define `CounterCompleted` as an immutable class or record implementing `IEvent`, with `EntityRef Entity` and `int Count` data. The example counts updates, so the real elapsed duration depends on the configured tick rate. Use `time.Delta.TotalSeconds` for behavior expressed in simulation seconds.

Removing the component takes effect when this callback finishes successfully. A later system in the same tick can see the removal. The completion event is delivered after all system updates finish.

## Receive the completion event

Create a listener system whose `OnStart` stores the subscription returned by this call.

```text
events.Subscribe<CounterCompleted>(HandleCompleted)
```

The handler signature is `void HandleCompleted(CounterCompleted data, SimulationTime time, ICommandBuffer commands)`. It can inspect `data.Entity`, print `data.Count`, or queue further changes. Check `Store.IsAlive(data.Entity)` before accessing entity data because references can become stale before event delivery.

Override `OnStop` and call `Dispose()` on the stored subscription. Register the listener system during `OnLoad`. This gives the subscription a clear lifetime and prevents abandoned listeners.

## Build and install

Run these commands from the repository root after creating the project.

```text
dotnet build ExampleMod/ExampleMod.csproj
dotnet build CitizenSimulator/CitizenSimulator.csproj
```

Copy `ExampleMod/bin/Debug/net10.0/ExampleMod.dll` into `CitizenSimulator/bin/Debug/net10.0/Mods/ExampleMod/`. Copy any additional required mod and helper assemblies into the `Mods` tree as well. Use the abstraction assembly supplied by the host and avoid distributing competing copies of shared assemblies.

Start the host with these commands from the repository root.

```text
cd CitizenSimulator
dotnet run
```

The loader always uses the executable base directory for `Mods`, regardless of the working directory.

Look for `[DISCOVERED]`, `[LOADING]`, and `[LOADED]` messages. Verify that the counter completion handler runs once and that removing the component prevents later updates from incrementing it.
