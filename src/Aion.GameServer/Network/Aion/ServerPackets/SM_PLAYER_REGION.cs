using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java SM_PLAYER_REGION: object ID, three zero bytes and the exact zone name's UTF-16 Java hash.</summary>
public class SM_PLAYER_REGION : AionServerPacket
{
    private readonly int playerObjId;
    private readonly string subZone;

    public SM_PLAYER_REGION(Player player, string subZone)
    {
        this.playerObjId = player.GetObjectId();
        this.subZone = subZone;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(playerObjId);
        WriteC(0);
        WriteC(0);
        WriteC(0);
        WriteD(JavaString.HashCode(subZone));
    }
}
