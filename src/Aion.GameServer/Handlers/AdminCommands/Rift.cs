using System;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Rift;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Rift.</summary>
public class Rift : AdminCommand
{
    public Rift()
        : base("rift", "Opens or closes rifts in the world.", """
            list - Lists all rift locations.
            open <location ID|world ID> [g] - Opens the rifts at the given location. If g is specified and spawns are defined, guards will spawn.
            close <location ID|world ID> - Closes the rifts at the given location.
            """)
    {
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length > 0 && "list".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            SendInfo(player, "Rift locations:");
            foreach (KeyValuePair<int, RiftLocation> entry in RiftService.GetInstance().GetRiftLocations())
                SendInfo(player, "ID: " + entry.Key + ", world ID: " + entry.Value.GetWorldId() + (entry.Value.IsOpened() ? " (open)" : ""));
        }
        else if (paramsArr.Length > 1 && "open".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            int id = ParseId(paramsArr[1]);
            bool guards = paramsArr.Length > 2 && paramsArr[2].Equals("g", StringComparison.OrdinalIgnoreCase);
            bool result = RiftService.GetInstance().OpenRifts(id, guards);
            SendInfo(player, result ? "Opened rifts at location " + id + "." : "Rifts are already open.");
        }
        else if (paramsArr.Length > 1 && "close".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            int id = ParseId(paramsArr[1]);
            bool result = RiftService.GetInstance().CloseRifts(ParseId(paramsArr[1]));
            SendInfo(player, result ? "Closed rifts at location " + id + "." : "Rifts were already closed.");
        }
        else
        {
            SendInfo(player);
        }
    }

    private int ParseId(string idParam)
    {
        int id = ParseInt(idParam);
        if (!RiftService.GetInstance().IsValidId(id))
            throw new ArgumentException("Invalid rift world ID or location ID.");
        return id;
    }
}
