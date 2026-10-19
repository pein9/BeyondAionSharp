using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.Utils.IdFactory;
using ItemDeleteType = global::Aion.GameServer.Services.Items.ItemPacketService.ItemDeleteType;
using PersistentState = global::Aion.GameServer.Model.GameObjects.IPersistable.PersistentState;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;
using Aion.GameServer.Model.Templates.Housing;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_HOUSE_EDIT (Rolandas). House decoration/renovation editor (enter/exit modes, add/delete/spawn/move/despawn objects, switch building). HouseObject&lt;?&gt; -> &lt;PlaceableHouseObject&gt;. HousingService/HouseObjectFactory/SM_HOUSE_EDIT red-tolerated.</summary>
public class CM_HOUSE_EDIT : AionClientPacket
{
    private HousingCommand? action;
    private int itemObjectId;
    private float x, y, z;
    private int rotation;
    private int buildingId;

    public CM_HOUSE_EDIT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        int actionId = ReadUC();
        action = HousingCommandExtensions.GetById(actionId);
        if (action == null)
        {
            AionLog.For(nameof(CM_HOUSE_EDIT)).LogWarning("{Player} sent unknown house edit action {Action}", GetConnection().GetActivePlayer(), actionId);
            return;
        }
        switch (action.Value)
        {
            case HousingCommand.REGISTER_ITEM:
            case HousingCommand.REMOVE_ITEM:
            case HousingCommand.WITHDRAW_OBJECT:
                itemObjectId = ReadD();
                break;
            case HousingCommand.PUT_OBJECT:
            case HousingCommand.MOVE_OBJECT:
                itemObjectId = ReadD();
                x = ReadF();
                y = ReadF();
                z = ReadF();
                rotation = ReadUH();
                break;
            case HousingCommand.CHANGE_BUILDING:
                buildingId = ReadD();
                break;
        }
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player == null || action == null)
            return;
        House house = player.GetActiveHouse();
        if (house == null)
            return;
        if (action != HousingCommand.END_EDIT && action != HousingCommand.END_BUILDING_EDIT && !house.IsOnLand(player))
            return;
        if ((action == HousingCommand.PUT_OBJECT || action == HousingCommand.MOVE_OBJECT) && !house.IsInsideLand(x, y))
        {
            AuditLogger.Log(player, "tried to place a house object at " + x + ", " + y + ", " + z + " outside of house " + house.GetAddress().GetId());
            return;
        }

        if (action == HousingCommand.BEGIN_EDIT)
        { // Enter Decoration mode
            SendPacket(new SM_HOUSE_EDIT(action.Value));
            SendPacket(new SM_HOUSE_REGISTRY(HousingContainer.OBJECTS));
            SendPacket(new SM_HOUSE_REGISTRY(HousingContainer.DECORATIONS));
        }
        else if (action == HousingCommand.END_EDIT)
        { // Exit Decoration mode
            SendPacket(new SM_HOUSE_EDIT(action.Value));
        }
        else if (action == HousingCommand.REGISTER_ITEM)
        { // Add item
            Item item = player.GetInventory().GetItemByObjId(itemObjectId);
            if (item == null)
                return;

            ItemTemplate template = item.GetItemTemplate();
            player.GetInventory().Delete(item, ItemDeleteType.REGISTER);

            DecorateAction decorateAction = template.GetActions().GetDecorateAction();
            if (decorateAction != null)
            {
                HouseDecoration decor = new HouseDecoration(IDFactory.GetInstance().NextId(), decorateAction.GetTemplateId());
                house.GetRegistry().PutDecor(decor, true);
                SendPacket(new SM_HOUSE_EDIT(action.Value, HousingContainer.DECORATIONS, decor.GetObjectId()));
            }
            else
            {
                HouseObject obj = HouseObjectFactory.CreateNew(house, template);
                house.GetRegistry().PutObject(obj, true);
                SendPacket(new SM_HOUSE_EDIT(action.Value, HousingContainer.OBJECTS, obj.GetObjectId()));
            }
        }
        else if (action == HousingCommand.REMOVE_ITEM)
        { // Delete item
            house.GetRegistry().DiscardObject(house.GetRegistry().GetObjectByObjId(itemObjectId), false);
            SendPacket(new SM_HOUSE_EDIT(action.Value, HousingContainer.OBJECTS, itemObjectId));
            SendPacket(new SM_HOUSE_EDIT(HousingCommand.REMOVE_ITEM, HousingContainer.OBJECTS, itemObjectId));
        }
        else if (action == HousingCommand.PUT_OBJECT)
        { // spawn object
            HouseObject obj = house.GetRegistry().GetObjectByObjId(itemObjectId);
            if (obj == null)
                return;
            obj.SetX(x);
            obj.SetY(y);
            obj.SetZ(z);
            obj.SetRotation(rotation);
            SendPacket(new SM_HOUSE_EDIT(action.Value, itemObjectId, x, y, z, rotation));
            obj.Spawn();
            house.GetRegistry().SetPersistentState(PersistentState.UPDATE_REQUIRED);
            SendPacket(new SM_HOUSE_EDIT(HousingCommand.REMOVE_ITEM, HousingContainer.OBJECTS, itemObjectId));
            global::Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnHouseItemUseEvent(new QuestEnv(null, player, 0));
        }
        else if (action == HousingCommand.MOVE_OBJECT)
        { // move object
            HouseObject obj = house.GetRegistry().GetObjectByObjId(itemObjectId);
            if (obj == null)
                return;
            SendPacket(new SM_HOUSE_EDIT(HousingCommand.WITHDRAW_OBJECT, itemObjectId));
            obj.GetController().Delete();
            obj.SetX(x);
            obj.SetY(y);
            obj.SetZ(z);
            obj.SetRotation(rotation);
            if (obj.GetPersistentState() == PersistentState.UPDATE_REQUIRED)
                house.GetRegistry().SetPersistentState(PersistentState.UPDATE_REQUIRED);
            SendPacket(new SM_HOUSE_EDIT(HousingCommand.PUT_OBJECT, itemObjectId, x, y, z, rotation));
            obj.Spawn();
        }
        else if (action == HousingCommand.WITHDRAW_OBJECT)
        { // despawn object
            HouseObject obj = house.GetRegistry().GetObjectByObjId(itemObjectId);
            if (obj == null)
                return;
            SendPacket(new SM_HOUSE_EDIT(action.Value, itemObjectId));
            obj.RemoveFromHouse();
            SendPacket(new SM_HOUSE_EDIT(HousingCommand.REGISTER_ITEM, HousingContainer.OBJECTS, itemObjectId)); // place it back
        }
        else if (action == HousingCommand.BEGIN_BUILDING_EDIT)
        { // enter renovation mode
            SendPacket(new SM_HOUSE_EDIT(HousingCommand.BEGIN_BUILDING_EDIT));
        }
        else if (action == HousingCommand.END_BUILDING_EDIT)
        { // exit renovation mode
            SendPacket(new SM_HOUSE_EDIT(HousingCommand.END_BUILDING_EDIT));
        }
        else if (action == HousingCommand.CHANGE_BUILDING)
        {
            if (!house.GetLand().GetBuildings().Any(building => building.GetId() == buildingId))
            {
                AuditLogger.Log(player, "attempted house renovation to building " + buildingId + " which doesn't belong to " + house.GetLand().GetId());
                return;
            }
            if (!RemoveRenovationCoupon(player, house))
            {
                AuditLogger.Log(player, "attempted house renovation without coupon");
                return;
            }
            HousingService.GetInstance().SwitchHouseBuilding(house, buildingId);
            house.GetController().UpdateAppearance();
        }
    }

    private bool RemoveRenovationCoupon(Player player, House house)
    {
        int typeId = house.GetHouseType().GetId();
        if (typeId == 0)
            return false; // studio
        int itemId = (player.GetRace().Equals(Race.ELYOS) ? 169661004 : 169661008) - typeId;
        if (player.GetInventory().GetItemCountByItemId(itemId) > 0)
            return player.GetInventory().DecreaseByItemId(itemId, 1);
        return false;
    }
}
