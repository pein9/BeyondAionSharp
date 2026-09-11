using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/HouseCommand (Rolandas).</summary>
public class HouseCommand : AdminCommand
{
    public HouseCommand()
        : base("house", "House teleport and ownership management.", """
            list - Shows house addresses for each map.
            tp <address> - Teleports you to the house with the given address.
            own <address> - Gives ownership of given house to your target.
            revoke <address> - Revokes ownership of given house.
            reloadscripts <address> - Reloads all scripts for the given house.
            """)
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length >= 1 && "list".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            ListHouses(admin);
        }
        else if (paramsArr.Length >= 2 && "own".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            AcquireHouse(admin, GetHouse(paramsArr[1]));
        }
        else if (paramsArr.Length >= 2 && "revoke".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            RevokeOwnership(admin, GetHouse(paramsArr[1]));
        }
        else if (paramsArr.Length >= 2 && "tp".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            House house = GetHouse(paramsArr[1]);
            TeleportService.TeleportTo(admin, house.GetWorldMapInstance(), house.GetX(), house.GetY(), house.GetZ(), (byte)house.GetTeleportHeading());
        }
        else if (paramsArr.Length >= 2 && "reloadscripts".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            ReloadPlayerScripts(admin, GetHouse(paramsArr[1]));
        }
        else
        {
            SendInfo(admin);
        }
    }

    private void ListHouses(Player admin)
    {
        // Java: groupingBy(address.mapId, TreeMap::new, toList()) - maps in ascending ID order, houses in encounter order.
        foreach (IGrouping<int, House> houses in HousingService.GetInstance().GetCustomHouses().GroupBy(house => house.GetAddress().GetMapId()).OrderBy(g => g.Key))
        {
            SendInfo(admin, "House addresses in " + WorldName(houses.Key) + ":");
            foreach (KeyValuePair<HouseType, List<House>> entry in GroupByType(houses.ToList()))
            {
                string houseTypeName = entry.Key.ToString();
                houseTypeName = houseTypeName[0] + houseTypeName.Substring(1).ToLowerInvariant();
                SendInfo(admin, "\t" + houseTypeName + ": " + FormatAddresses(entry.Value));
            }
        }
    }

    private string FormatAddresses(List<House> houses)
    {
        bool dash = false;
        int lastAddress = houses[0].GetAddress().GetId();
        string addresses = ChatUtil.Color(lastAddress + "", Color.White);
        for (int i = 1; i < houses.Count; i++)
        {
            House house = houses[i];
            int currentAddress = house.GetAddress().GetId();
            if (lastAddress + 1 != currentAddress || i + 1 == houses.Count || currentAddress + 1 != houses[i + 1].GetAddress().GetId())
            {
                if (!string.IsNullOrEmpty(addresses) && !dash)
                    addresses += ", ";
                addresses += ChatUtil.Color(currentAddress + "", Color.White);
                dash = false;
            }
            else if (!dash)
            {
                addresses += "-";
                dash = true;
            }
            lastAddress = house.GetAddress().GetId();
        }
        return addresses;
    }

    private Dictionary<HouseType, List<House>> GroupByType(List<House> houses)
    {
        // Java parity: comparator = comparing(houseType.id).reversed().thenComparing(address.id), grouped into a LinkedHashMap
        List<House> sorted = houses
            .OrderByDescending(house => house.GetHouseType().GetId())
            .ThenBy(house => house.GetAddress().GetId())
            .ToList();
        Dictionary<HouseType, List<House>> result = new Dictionary<HouseType, List<House>>();
        foreach (House house in sorted)
        {
            if (!result.TryGetValue(house.GetHouseType(), out List<House> list))
            {
                list = new List<House>();
                result[house.GetHouseType()] = list;
            }
            list.Add(house);
        }
        return result;
    }

    private House GetHouse(string param)
    {
        int address = ParseInt(param);
        // Java: Objects.requireNonNull(house, "Invalid address.") throws a NullPointerException, which the command framework logs instead of
        // showing it to the player (it is not an IllegalArgumentException), so this must not be an ArgumentException either.
        return HousingService.GetInstance().GetHouseByAddress(address) ?? throw new NullReferenceException("Invalid address.");
    }

    private void AcquireHouse(Player admin, House house)
    {
        VisibleObject creature = admin.GetTarget();
        if (!(creature is Player target))
        {
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
            return;
        }

        if (house.GetOwnerId() == target.GetObjectId())
        {
            SendInfo(admin, Name(target) + " already owns that house.");
            return;
        }
        if (target.GetHouses().Count >= 2)
        {
            SendInfo(admin, Name(target) + " must sell his old house which is currently in grace time first!");
            return;
        }
        House studio = HousingService.GetInstance().GetPlayerStudio(target.GetObjectId());
        if (studio != null)
            HousingService.GetInstance().ChangeOwner(studio, 0);
        HousingService.GetInstance().ChangeOwner(house, target.GetObjectId());
        SendInfo(admin, "House " + house.GetName() + " is now owned by " + Name(target));
    }

    private void RevokeOwnership(Player admin, House house)
    {
        int ownerId = house.GetOwnerId();
        if (ownerId == 0)
        {
            SendInfo(admin, "House has no owner.");
            return;
        }
        HousingService.GetInstance().ChangeOwner(house, 0);
        SendInfo(admin, "Ownership of house " + house.GetAddress().GetId() + " was revoked from " + PlayerService.GetPlayerName(ownerId));
    }

    private void ReloadPlayerScripts(Player admin, House house)
    {
        Npc butler = house.GetButler();
        if (butler == null)
        {
            SendInfo(admin, "No butler was found for house with address " + house.GetAddress().GetId());
            return;
        }
        house.ReloadPlayerScripts();
        butler.GetKnownList().ForEachPlayer(house.SendScripts);
        SendInfo(admin, "Script reload successful");
    }
}
