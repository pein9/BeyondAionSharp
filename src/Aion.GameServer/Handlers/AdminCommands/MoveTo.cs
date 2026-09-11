using System;
using System.Text.RegularExpressions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/MoveTo (Neon).</summary>
public class MoveTo : AdminCommand
{
    public MoveTo()
        : base("moveto", "Moves you to any location.", """
            <x> <y> [z] - Moves you to the specified coordinates on the current map (also supports pasted xml attributes like x="1422.7744" y="1250.0612" z="569.47").
            <map name|ID> <x> <y> [z] - Moves you to the specified position (map names need underscores instead of spaces).
            <position link> - Moves you to the position of the chat link.
            <player name> - Moves you to the player.
            <npc name|ID> - Moves you to a spawn spot of the NPC.
            forward <distance> - Moves you forward by the specified distance, ignoring any obstacles in between.
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
        string errorMsg = null;
        if (paramsArr.Length == 2 && "forward".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            MoveForward(admin, ParseFloat(paramsArr[1]));
            return;
        }
        WorldPosition pos = paramsArr.Length == 1 ? ChatUtil.GetPosition(paramsArr[0]) : ParseWorldPosition(admin, paramsArr);
        if (pos != null)
        {
            pos.SetH(admin.GetHeading());
            DoMoveTo(admin, pos, "Teleported to " + WorldName(pos.GetMapId()) + "\nX:" + pos.GetX() + " Y:" + pos.GetY() + " Z:" + pos.GetZ());
            return;
        }
        else if (paramsArr.Length > 1 || paramsArr[0].StartsWith("[pos:"))
            errorMsg = $"Invalid map position or {(GeoDataConfig.GEO_ENABLE ? "missing" : "deactivated")} geo.";

        string nameOrId = string.Join(" ", paramsArr).ToLowerInvariant();
        Player player = World.World.GetInstance().GetPlayer(Util.ConvertName(nameOrId));
        if (player != null && !player.Equals(admin))
        {
            DoMoveTo(admin, player.GetPosition(), "Teleported to " + Name(player) + ".");
            return;
        }
        else if (errorMsg == null || admin.Equals(player))
        {
            errorMsg = "Invalid player name or player is offline.";
        }

        int npcId = GetNpcId(nameOrId);
        if (npcId > 0 && DataManager.SPAWNS_DATA.GetFirstSpawnByNpcId(0, npcId) != null)
        {
            SendInfo(admin, "Teleported to " + ChatUtil.Path(npcId, true) + ".");
            TeleportService.TeleportToNpc(admin, npcId);
            return;
        }
        else if (npcId > 0)
        {
            errorMsg = "Could not find " + ChatUtil.Path(npcId, true) + ".";
        }
        else if (nameOrId.Contains(' '))
        {
            errorMsg = "Could not find \"" + nameOrId + "\".";
        }

        SendInfo(admin, errorMsg);
    }

    private static void MoveForward(Player admin, float distance)
    {
        (float x, float y) = CalculateForwardPosition(admin.GetX(), admin.GetY(), admin.GetHeading(), distance);
        admin.GetPosition().SetXYZH(x, y, admin.GetZ(), admin.GetHeading());
        PacketSendUtility.BroadcastToSightedPlayers(admin, new SM_POSITION(admin), true);
    }

    internal static (float X, float Y) CalculateForwardPosition(float x, float y, byte heading, float distance)
    {
        double radians = Math.PI / 180 * PositionUtil.ConvertHeadingToAngle(heading);
        return ((float)(x + Math.Cos(radians) * distance), (float)(y + Math.Sin(radians) * distance));
    }

    private WorldPosition ParseWorldPosition(Player admin, string[] paramsArr)
    {
        int coordIndex = 0;
        int mapId;
        bool isMapId = Regex.IsMatch(paramsArr[0], "^[1-9][0-9]{8,}$");
        bool isMapName = Regex.IsMatch(paramsArr[0], "^[a-zA-Z_]+$");
        if (isMapId || isMapName)
        {
            mapId = isMapId ? ParseInt(paramsArr[0]) : WorldMapTypeExtensions.GetMapId(paramsArr[0]);
            coordIndex = 1;
        }
        else
        {
            mapId = EncodeMapAndInstanceId(admin.GetWorldId(), admin.GetInstanceId());
        }
        float? x = null, y = null, z = null;
        Regex p = new Regex("^((?<type>x|y|z)(=|:)\"?)?(?<coord>[0-9]+(\\.[0-9]+)?f?)\"?,?$", RegexOptions.IgnoreCase);
        int maxIndex = Math.Min(paramsArr.Length, coordIndex + 3);
        for (int i = coordIndex; i < maxIndex; i++)
        {
            Match m = p.Match(paramsArr[i]);
            if (m.Success)
            {
                float coord = ParseFloat(m.Groups["coord"].Value);
                string type = m.Groups["type"].Success ? m.Groups["type"].Value : null;
                if ("x".Equals(type, StringComparison.OrdinalIgnoreCase) || (x == null && type == null))
                    x = coord;
                else if ("y".Equals(type, StringComparison.OrdinalIgnoreCase) || (y == null && type == null))
                    y = coord;
                else if ("z".Equals(type, StringComparison.OrdinalIgnoreCase) || (z == null && type == null))
                    z = coord;
            }
            else
            {
                return null;
            }
        }
        return x == null || y == null ? null : ChatUtil.ParsedCoordsToWorldPosition(mapId, x.Value, y.Value, z, null);
    }

    internal static int EncodeMapAndInstanceId(int worldId, int instanceId) => worldId + instanceId - 1;

    private void DoMoveTo(Player admin, WorldPosition pos, string message)
    {
        SendInfo(admin, message); // msg before teleport, otherwise client could ignore it
        if (pos.IsInstanceMap() && pos.GetWorldMapInstance().GetParent().GetMainWorldMapInstance() == pos.GetWorldMapInstance())
        {
            // currently instance type maps have a main world map instance (default) which have no spawns, so we create a new instance instead
            WorldMapInstance instance = InstanceService.GetOrRegisterInstance(pos.GetMapId(), admin);
            pos = World.World.GetInstance().CreatePosition(pos.GetMapId(), pos.GetX(), pos.GetY(), pos.GetZ(), pos.GetHeading(), instance.GetInstanceId());
        }
        TeleportService.TeleportTo(admin, pos);
    }

    private int GetNpcId(string nameOrId)
    {
        if (Regex.IsMatch(nameOrId, "^[1-9][0-9]{5}$"))
            return ParseInt(nameOrId);
        foreach (NpcTemplate template in DataManager.NPC_DATA.GetNpcData())
        {
            if (template.GetName().ToLowerInvariant().Equals(nameOrId))
                return template.GetTemplateId();
        }
        return 0;
    }
}
