using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Modding;

namespace CitizenSimulator.Engine.Modding;

public sealed class ModContext : IModContext
{
    public IEntityStore Store { get; }

    public IComponentRegistry Components { get; }

    public ISystemRegistry Systems { get; }

    public ModContext(IEntityStore store, IComponentRegistry components, ISystemRegistry systems)
    {
        Store = store;
        Components = components;
        Systems = systems;
    }
}