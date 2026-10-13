using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Custom.Companion;

/// <summary>
/// <c>.companion</c> (D41, docs/companion-bots.md): no Java counterpart. It is not public, so the command loader does
/// not register it; <see cref="CompanionService.Enable"/> does, and only a server that switched companions on has it.
/// </summary>
internal sealed class CompanionCommand : PlayerCommand
{
    public CompanionCommand()
        : base(CompanionService.Alias, "Brings a companion to fight or heal beside you.", """
            <class> - Brings a level 26 companion of that class.
            list - Names your companions.
            dismiss [name] - Sends one companion away, or all of them.
            """)
    {
    }

    /// <summary>While companions are off this is no command at all: the words go out as ordinary chat.</summary>
    public override bool ValidateAccess(Player player)
    {
        return CompanionService.GetInstance().Enabled;
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        CompanionService service = CompanionService.GetInstance();
        if (paramsArr.Length == 0)
        {
            SendInfo(player);
            return;
        }
        switch (paramsArr[0].ToLowerInvariant())
        {
            case "list":
                IReadOnlyList<CompanionInfo> own = service.Of(player);
                SendInfo(player, own.Count == 0
                    ? "You have no companion."
                    : string.Join("\n", own.Select(companion => companion.Name + ", " + CompanionService.Word(companion.PlayerClass)
                        + (companion.State == CompanionState.Ending ? ", leaving" : ""))));
                break;
            case "dismiss":
                SendInfo(player, service.Dismiss(player, paramsArr.Length > 1 ? paramsArr[1] : null));
                break;
            // The two words a companion itself says. A player who says them is told that they are not for players.
            case "come":
                SendInfo(player, service.BringToLeader(player));
                break;
            case "restock":
                SendInfo(player, service.Restock(player));
                break;
            default:
                SendInfo(player, service.Request(player, Join(paramsArr, 0)));
                break;
        }
    }
}
