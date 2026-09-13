using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates.World;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_L2AUTH_LOGIN_CHECK (-Nemesiss-). Login auth result, server index tables and the world-map list.</summary>
public class SM_L2AUTH_LOGIN_CHECK : AionServerPacket
{
    private static readonly byte[] serverIdByIndex = new byte[128];
    private static readonly byte[] serverIndexById = new byte[64];

    static SM_L2AUTH_LOGIN_CHECK()
    {
        // retail data, don't question it
        for (byte i = 1; i <= 60; i++)
        {
            serverIdByIndex[i] = i;
            serverIndexById[i] = i;
        }
        serverIdByIndex[66] = 61;
        serverIndexById[61] = 66;
    }

    /// <summary>
    /// True if client is authed.
    /// </summary>
    private readonly bool ok;
    private readonly string accountName;

    public SM_L2AUTH_LOGIN_CHECK(bool ok, string accountName)
    {
        this.ok = ok;
        this.accountName = accountName;
    }

    // Java parity (upstream c5a0f34c0): the generated server tables are byte-identical to the former 580 byte standardData literal.
    protected override void WriteImpl(AionConnection con)
    {
        WriteD(ok ? 0x00 : 0x01);
        WriteC(0); // server ID override (added in 4.7)
        WriteC(0); // 1 on Fast-Track Server: makes the client send C_REQUEST_DIRECT_ENTER_WORLD
        WriteC(0); // 1 on Fast-Track Server: displays the origin server's name above the minimap and as system message
        WriteC(0); // 1 on Fast-Track Server
        for (int i = 0; i < serverIdByIndex.Length; i++)
        {
            byte serverId = serverIdByIndex[i];
            WriteC(serverId == 0 ? 0 : i);
            WriteC(serverId);
            WriteC(serverId);
        }
        for (int serverId = 0; serverId < serverIndexById.Length; serverId++)
        {
            byte i = serverIndexById[serverId];
            WriteC(i);
            WriteC(i == 0 ? 0 : serverId);
            WriteC(i == 0 ? 0 : serverId);
        }
        WriteH(DataManager.WORLD_MAPS_DATA.Size());
        foreach (WorldMapTemplate template in DataManager.WORLD_MAPS_DATA)
        {
            WriteD(template.GetMapId());
            WriteH(template.IsInstance() ? 0 : template.GetTwinCount()); // for Fast-Track Server it's getBeginnerTwinCount()
        }
        WriteS(accountName);
    }
}
