using System;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Announce (Neon).</summary>
public class Announce : AdminCommand
{
    public Announce()
        : base("announce", "Sends a server-wide notice.", """
            n <message> - Sends the message with your name.
            a <message> - Sends the message anonymously.
            ely <message> - Sends an anonymous message to all Elyos players.
            asmo <message> - Sends an anonymous message to all Asmodian players.
            """)
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length <= 1)
        {
            SendInfo(admin);
            return;
        }
        string message;
        Race? allowedRace = null;
        if ("n".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            message = Name(admin) + ": ";
        }
        else if ("a".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            message = "Announce: ";
        }
        else if ("ely".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            message = "Elyos: ";
            allowedRace = Race.ELYOS;
        }
        else if ("asmo".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            message = "Asmodians: ";
            allowedRace = Race.ASMODIANS;
        }
        else
        {
            SendInfo(admin);
            return;
        }
        message += Join(paramsArr, 1);
        foreach (Player player in Aion.GameServer.World.World.GetInstance().GetAllPlayers())
            if (allowedRace == null || player.GetRace() == allowedRace || ValidateAccess(player))
                PacketSendUtility.SendMessage(player, message, ChatType.BRIGHT_YELLOW_CENTER);
    }
}
