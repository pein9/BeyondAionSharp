using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Ban;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Gag (Watson, Neon).</summary>
public class Gag : AdminCommand
{
    public Gag()
        : base("gag", "Bans a player from all chats.", """
            <player> <duration> <reason> - Chat bans the player for the specified time in minutes.
            <player> remove - Removes the chat ban of this player.
            """)
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 2)
        {
            SendInfo(admin);
            return;
        }
        string playerName = Util.ConvertName(paramsArr[0]);
        Player player = Aion.GameServer.World.World.GetInstance().GetPlayer(playerName);
        if (player == null)
        {
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_NO_SUCH_USER(playerName));
            return;
        }
        if (paramsArr[1].Equals("remove", System.StringComparison.OrdinalIgnoreCase))
        {
            if (ChatBanService.IsBanned(player))
            {
                ChatBanService.UnbanPlayer(player);
                SendInfo(admin, "Unbanned " + Name(player) + " from all chats.");
            }
            else
            {
                SendInfo(admin, Name(player) + " can already chat.");
            }
        }
        else
        {
            int durationMinutes = ParseInt(paramsArr[1]);
            if (durationMinutes < 1)
            {
                SendInfo(admin, "Duration must be at least 1 minute.");
                return;
            }
            string reason = Join(paramsArr, 2);
            if (reason.Length == 0)
            {
                SendInfo(admin, "Reason must be specified.");
                return;
            }
            ChatBanService.BanPlayer(player, (long)System.TimeSpan.FromMinutes(durationMinutes).TotalMilliseconds);
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_INGAME_BLOCK_ENABLE_NO_CHAT(durationMinutes));
            SendInfo(player, reason);
            SendInfo(admin, Name(player) + " is now gagged for " + durationMinutes + " minute(s).");
        }
    }
}
