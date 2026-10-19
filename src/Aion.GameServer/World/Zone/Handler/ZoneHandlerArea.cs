using System;

namespace Aion.GameServer.World.Zone.Handler;

/// <summary>Java ZoneHandlerArea: exact names of static areas handled by this class.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ZoneHandlerArea : Attribute
{
    public string[] Value { get; }
    public ZoneHandlerArea(params string[] value) => Value = value;
}
