using Aion.GameServer.Network.Aion;
using Aion.GameServer.Model.Templates.Flypath;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_WINDSTREAM. Windstream state acknowledgement.</summary>
public class SM_WINDSTREAM : AionServerPacket
{
    private readonly WindstreamState state;
    private readonly bool success;

    public SM_WINDSTREAM(WindstreamState state, bool success)
    {
        this.state = state;
        this.success = success;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD((int)state);
        WriteC(success ? 1 : 0);
    }
}
