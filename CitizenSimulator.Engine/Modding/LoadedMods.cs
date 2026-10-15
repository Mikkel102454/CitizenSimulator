using CitizenSimulator.Abstractions.Modding;

namespace CitizenSimulator.Engine.Modding;

public sealed record LoadedMod(
    IMod Instance,
    ModAttribute Metadata
);