using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_READ_EXPRESS_MAIL (antness, Guapo). Closes postman (0) or summons it on icon click (1) with express/blackcloud unread + cooldown handling. Timestamp cooldowns use SystemClock; blackcloud mail bypasses refusal.</summary>
public class CM_READ_EXPRESS_MAIL : AionClientPacket
{
    private static readonly ILogger log = AionLog.For(nameof(CM_READ_EXPRESS_MAIL));
    private static readonly ConcurrentDictionary<int, long> cooldownByPlayer = new();
    private byte action;

    public CM_READ_EXPRESS_MAIL(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        action = ReadC();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();

        switch (action)
        {
            case 0: // window is closed
                if (player.GetPostman() != null)
                    player.GetPostman().GetController().Delete();
                break;
            case 1: // click on icon
                bool haveUnreadExpress = player.GetMailbox().HaveUnreadByType(LetterType.EXPRESS);
                bool haveUnreadBlackcloud = player.GetMailbox().HaveUnreadByType(LetterType.BLACKCLOUD);
                if (player.GetPostman() != null)
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_POSTMAN_ALREADY_SUMMONED());
                }
                else if (player.IsFlying())
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_POSTMAN_UNABLE_IN_FLIGHT());
                }
                else if (!haveUnreadExpress && !haveUnreadBlackcloud)
                {
                    return;
                }
                else if (!haveUnreadBlackcloud && cooldownByPlayer.GetValueOrDefault(player.GetObjectId(), 0L) > SystemClock.CurrentMillis())
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_POSTMAN_UNABLE_IN_COOLTIME());
                }
                else
                {
                    VisibleObjectSpawner.SpawnPostman(player);
                    StartPostmanCooldown(player);
                }
                break;
            default:
                log.LogWarning("{Player} sent unknown read express mail action type: {Action}", player, action);
                break;
        }
    }
    public static void StartPostmanCooldown(Player player)
    {
        long now = SystemClock.CurrentMillis();
        cooldownByPlayer[player.GetObjectId()] = unchecked(now + 600000L);
        foreach (var entry in cooldownByPlayer)
            if (entry.Value < now)
                ((ICollection<KeyValuePair<int, long>>)cooldownByPlayer).Remove(entry);
    }
}
