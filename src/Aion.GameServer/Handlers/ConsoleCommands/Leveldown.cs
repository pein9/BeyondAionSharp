using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Leveldown (ginho1, Neon). Levels the selected player down.</summary>
public class Leveldown : ConsoleCommand
{
    public Leveldown()
        : base("leveldown", "Levels a player down.", """
            <value> - Levels your target down by the specified number of levels (defaults to your character, if no player is targeted).
            """)
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 1)
        {
            SendInfo(admin);
            return;
        }
        Player player = admin.GetTarget() is Player target ? target : admin;
        int newLevel = player.GetLevel() - ParseInt(paramsArr[0]);
        if (newLevel < 1 || newLevel > GSConfig.PLAYER_MAX_LEVEL)
        {
            SendInfo(admin, "Invalid level.");
            return;
        }
        player.GetCommonData().SetLevel(newLevel);
        SendInfo(admin, "Set " + Name(player) + "'s level to " + player.GetLevel());
    }
}
