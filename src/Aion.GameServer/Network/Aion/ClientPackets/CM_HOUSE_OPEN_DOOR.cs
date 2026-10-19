using System.Collections.Generic;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_HOUSE_OPEN_DOOR (Rolandas, Neon). Enter/leave a house via its door (client decides direction). HousingService/TeleportService red-tolerated.</summary>
public class CM_HOUSE_OPEN_DOOR : AionClientPacket
{
    private const float MAX_DISTANCE_TO_HOUSE = 40;
    private int address;
    private bool leave;

    public CM_HOUSE_OPEN_DOOR(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        address = ReadD();
        leave = ReadC() != 0;
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player == null)
            return;

        House house = HousingService.GetInstance().GetHouseByAddress(address);
        if (house == null)
            return;

        if (!IsAtHouse(player, house))
        {
            AuditLogger.Log(player, "tried to use the door of house " + address + " from " + player.GetPosition());
            return;
        }
        if (leave)
        {
            if (house.GetAddress().GetExitMapId() != null)
            {
                TeleportService.TeleportTo(player, house.GetAddress().GetExitMapId().Value, house.GetAddress().GetExitX(), house.GetAddress().GetExitY(),
                    house.GetAddress().GetExitZ(), (byte)0, TeleportAnimation.FADE_OUT_BEAM);
            }
            else
            {
                house.GetController().TeleportNearHouseDoor(player, true);
            }
        }
        else
        {
            if (player.HasAccess(AdminConfig.HOUSE_SHOW_ADDRESS))
                PacketSendUtility.SendMessage(player, "House address: " + address);
            if (!house.CanEnter(player))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_ENTER_NO_RIGHT2());
                return;
            }
            house.GetController().TeleportNearHouseDoor(player, false);
        }
    }

    private bool IsAtHouse(Player player, House house)
    {
        if (house.GetAddress().GetExitMapId() != null)
            return player.GetWorldId() == house.GetAddress().GetMapId();
        return house.IsSpawned() && PositionUtil.IsInRange(player, house, MAX_DISTANCE_TO_HOUSE);
    }

}
