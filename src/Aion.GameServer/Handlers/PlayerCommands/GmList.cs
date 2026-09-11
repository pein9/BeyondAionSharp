using System.Collections.Generic;
using System.Text;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.PlayerCommands;

/// <summary>Java parity: data/handlers/playercommands/GmList (Aion Gates, Neon). Lists online, whisper-enabled team members.</summary>
public class GmList : PlayerCommand
{
    public GmList()
        : base("gmlist", "Lists all available team members.")
    {
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        List<Player> availableStaffMembers = GMService.GetInstance().GetAvailableStaffMembers();
        if (availableStaffMembers.Count == 0)
        {
            SendInfo(player, "There is no GM online.");
            return;
        }
        StringBuilder sb = new StringBuilder("GMs online (" + availableStaffMembers.Count + "):");
        foreach (Player gm in availableStaffMembers)
        {
            FriendList.Status status = gm.GetFriendList().GetStatus();
            sb.Append("\n\t").Append(Name(gm)).Append(" (").Append(status.ToString().ToLowerInvariant()).Append(")");
        }
        SendInfo(player, sb.ToString());
    }
}
