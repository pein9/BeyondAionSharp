using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.QuestEngine.Task.Checker;

/// <summary>Java parity: questEngine/task/checker/ZoneChecker.</summary>
public class ZoneChecker : DestinationChecker
{
    protected readonly string zoneName;

    public ZoneChecker(Creature follower, string zoneName)
        : base(follower)
    {
        this.zoneName = zoneName;
    }

    public override bool Check()
    {
        return follower.IsInsideZone(zoneName);
    }
}
