using Aion.GameServer.Handlers.AdminCommands;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Itemcooltime (Neon). Removes cooldowns of all items.</summary>
public class Itemcooltime : ConsoleCommand
{
    public Itemcooltime()
        : base("itemcooltime", "Removes cooldowns of all items.")
    {
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        RemoveCd.RemoveItemCooldowns(player);
    }
}
