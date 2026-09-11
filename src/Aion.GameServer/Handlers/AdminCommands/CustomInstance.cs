using System;
using Aion.GameServer.Custom.Instance;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/CustomInstance (Estrayl). Utility command for the custom instance.</summary>
public class CustomInstance : AdminCommand
{
    public CustomInstance()
        : base("cinstance", "Utility command for the custom instance.", """
            removecd - Removes the custom instance cooldown of selected player.
            getrank - Gets the current custom instance rank of selected player.
            setrank [newRank] - Changes the custom instance rank of selected player to given value.
            """)
    {
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length < 1)
        {
            SendInfo(player);
            return;
        }
        if (player.GetTarget() is not Player target)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
            return;
        }
        if ("removecd".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            if (CustomInstanceService.GetInstance().ResetEntryCooldown(target.GetObjectId()))
            {
                SendInfo(player, "Removed custom instance cooldown for " + Name(target) + ".");
            }
            else
            {
                SendInfo(player, Name(target) + " does not need a reset.");
            }
        }
        else if ("getrank".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            int rank = CustomInstanceService.GetInstance().LoadOrCreateRank(target.GetObjectId()).GetRank();
            SendInfo(player, Name(target) + "'s current rank is " + CustomInstanceRankEnumExtensions.GetRankDescription(rank) + " (" + rank + ").");
        }
        else if ("setrank".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase) && paramsArr.Length > 1)
        {
            int rank = ParseInt(paramsArr[1]);
            CustomInstanceService.GetInstance().ChangePlayerRank(target.GetObjectId(), rank, 0);
            SendInfo(player, "Changed " + Name(target) + " to " + rank + " which is equivalent to " + CustomInstanceRankEnumExtensions.GetRankDescription(rank));
        }
        else
        {
            SendInfo(player);
        }
    }
}
