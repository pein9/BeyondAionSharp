using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>
/// Java parity: network/aion/serverpackets/SM_INVINCIBLE_TIME (SVDNESS). This packet is sent alongside SM_PLAYER_STATE to the protected player on
/// retail. It does not have any effect on the client. SM_PLAYER_STATE is what controls the blinking.
/// </summary>
public class SM_INVINCIBLE_TIME : AionServerPacket
{
    private readonly int timeMs;

    public SM_INVINCIBLE_TIME(int timeMs)
    {
        this.timeMs = timeMs;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(timeMs);
    }
}
