using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.Abstractions.Modding;

public interface IModContext
{
    IEntityStore Store { get; }

    IComponentRegistry Components { get; }

    ISystemRegistry Systems { get; }
}