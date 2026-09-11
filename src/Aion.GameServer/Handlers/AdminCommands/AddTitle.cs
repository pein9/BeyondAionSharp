using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.Utils.Extensions;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/AddTitle (xavier). Adds titles to players.</summary>
public class AddTitle : AdminCommand
{
    public AddTitle()
        : base("addtitle", "Adds titles to players.", """
            <title ID> - Adds the title to your target (defaults to your character, if no player is targeted).
            <title ID> <player> - Adds the title to the specified player.
            """)
    {
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length < 1 || paramsArr.Length > 2)
        {
            SendInfo(player);
            return;
        }

        TitleTemplate titleTemplate = DataManager.TITLE_DATA.GetTitleTemplate(ParseInt(paramsArr[0]));
        if (titleTemplate == null)
        {
            SendInfo(player, "Invalid title ID.");
            return;
        }

        Player target;
        if (paramsArr.Length == 2)
        {
            string playerName = Util.ConvertName(paramsArr[1]);
            target = Aion.GameServer.World.World.GetInstance().GetPlayer(playerName);
            if (target == null)
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_NO_SUCH_USER(playerName));
                return;
            }
        }
        else
        {
            target = player.GetTarget() is Player playerTarget ? playerTarget : player;
        }

        if (!target.GetTitleList().AddTitle(titleTemplate.GetTitleId(), false, 0))
        {
            if (!target.Equals(player))
                SendInfo(player, "Couldn't add title \"" + titleTemplate.GetL10n() + "\" to " + Name(target));
        }
        else
        {
            if (!target.Equals(player))
            {
                SendInfo(player, "Added title \"" + titleTemplate.GetL10n() + "\" to " + Name(target));
                SendInfo(target, Name(player) + " gave you the title \"" + titleTemplate.GetL10n() + "\"");
            }
        }
    }
}
