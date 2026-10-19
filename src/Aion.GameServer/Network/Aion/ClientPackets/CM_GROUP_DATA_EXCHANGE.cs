using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: bounded client broadcasts to the requested team or the sender's house land (6dd3aa507).</summary>
public class CM_GROUP_DATA_EXCHANGE : AionClientPacket
{
    private const int MAX_EXCHANGE_DATA_SIZE = 6144;
    private ClientBroadcastType? type;
    private Team? team;
    private int tag;
    private int dataSize;
    private byte[]? data;

    public CM_GROUP_DATA_EXCHANGE(int opcode, ISet<State> validStates) : base(opcode, validStates) { }

    protected override void ReadImpl()
    {
        type = ClientBroadcastTypeExtensions.GetById(ReadUC());
        if (type == ClientBroadcastType.TEAM)
        {
            team = GetTeamById(ReadUC());
            tag = ReadUC();
        }
        dataSize = ReadD();
        if (dataSize >= 0 && dataSize <= MAX_EXCHANGE_DATA_SIZE && dataSize <= GetRemainingBytes())
            data = ReadB(dataSize);
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player == null)
            return;
        if (data == null)
        {
            AuditLogger.Log(player, "sent client broadcast data of " + dataSize + " bytes (type " + (type?.ToString() ?? "null") + ")");
            return;
        }
        if (data.Length == 0 || type == null)
            return;
        if (type != ClientBroadcastType.TEAM)
        {
            BroadcastOnHouseLand(player);
            return;
        }
        List<Player>? players = team switch
        {
            Team.GROUP => player.IsInGroup() ? player.GetPlayerGroup().GetOnlineMembers() : null,
            Team.ALLIANCE => player.IsInAlliance() ? player.GetPlayerAlliance().GetOnlineMembers() : null,
            Team.LEAGUE => player.IsInLeague() ? player.GetPlayerAlliance().GetLeague().GetOnlineMembers() : null,
            _ => null
        };
        if (players == null || players.Count == 0)
            return;
        var packet = new SM_GROUP_DATA_EXCHANGE(tag, data);
        foreach (Player member in players)
        {
            if (!member.Equals(player))
                PacketSendUtility.SendPacket(member, packet);
        }
    }

    private void BroadcastOnHouseLand(Player player)
    {
        // Like Java, all three house types use the first HOUSE_ zone; inside-house geometry is not separate.
        string? land = player.GetPosition().GetMapRegion()!.FindZones(player).Select(zone => zone.GetZoneTemplate().GetName())
            .FirstOrDefault(name => name!.StartsWith("HOUSE_", StringComparison.Ordinal));
        if (land != null)
            PacketSendUtility.BroadcastPacket(player, new SM_GROUP_DATA_EXCHANGE(data!), true, other => other.IsInsideZone(land));
    }

    private enum Team { GROUP = 0, ALLIANCE = 1, LEAGUE = 2 }
    private static Team? GetTeamById(int id) => id switch { 0 or 1 or 2 => (Team)id, _ => null };
}
