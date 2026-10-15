using System.ComponentModel;
using CitizenSimulator.Abstractions.ECS;

namespace CitizenSimulator.TestMod.Components;

public sealed class MessageComponent : Component<MessageComponent>
{
    public string Message { get; set; } = "";
}