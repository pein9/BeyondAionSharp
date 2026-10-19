using Aion.GameServer.Model;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java 6dd3aa507: team data preserves the client's tag; house data always uses HOUSE.</summary>
public class SM_GROUP_DATA_EXCHANGE : AionServerPacket
{
    private readonly ClientBroadcastType type;
    private readonly int tag;
    private readonly byte[] data;

    public SM_GROUP_DATA_EXCHANGE(int tag, byte[] data) : this(ClientBroadcastType.TEAM, tag, data) { }
    public SM_GROUP_DATA_EXCHANGE(byte[] data) : this(ClientBroadcastType.HOUSE, 0, data) { }

    private SM_GROUP_DATA_EXCHANGE(ClientBroadcastType type, int tag, byte[] data)
    {
        this.type = type;
        this.tag = tag;
        this.data = data;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC((int)type);
        if (type == ClientBroadcastType.TEAM)
            WriteC(tag);
        WriteD(data.Length);
        WriteB(data);
    }
}
