using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Coords (Estrayl). Shows the target's current coordinates.</summary>
public class Coords : AdminCommand
{
    public Coords()
        : base("coords", "Shows the target's current coordinates.")
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        VisibleObject target = admin.GetTarget() == null ? admin : admin.GetTarget();
        SendInfo(admin, Name(target) + "'s position:\n" + target.GetPosition().ToCoordString().Replace(", X:", "\nX:"));
    }
}
