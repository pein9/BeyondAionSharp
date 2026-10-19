using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Restrictions;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Explicit enchanting/socket services; actual item actions and restrictions determine eligibility.</summary>
public class CM_MANASTONE : AionClientPacket
{
    private Action? action;
    private int targetFusedSlot;
    private int targetItemUniqueId;
    private int stoneUniqueId;
    private int supplementUniqueId;
    private int slotNum;
    private int npcObjId;

    public CM_MANASTONE(int opcode, ISet<State> validStates) : base(opcode, validStates) { }

    protected override void ReadImpl()
    {
        action = ReadUC() switch {1 => Action.ENCHANT, 2 => Action.SOCKET_MANASTONE, 3 => Action.REMOVE_MANASTONE, 4 => Action.SOCKET_GODSTONE, 8 => Action.AMPLIFY, _ => null};
        targetFusedSlot = ReadUC();
        targetItemUniqueId = ReadD();
        if (action == Action.REMOVE_MANASTONE)
        {
            slotNum = ReadD(); npcObjId = ReadD();
        }
        else
        {
            stoneUniqueId = ReadD(); supplementUniqueId = ReadD();
        }
    }

    protected override void RunImpl()
    {
        if (action == null) return;
        Player player = GetConnection().GetActivePlayer();
        player.GetController().CancelCurrentSkill(null!);
        player.GetController().CancelUseItem();
        switch (action)
        {
            case Action.ENCHANT:
            case Action.SOCKET_MANASTONE:
            {
                Item stone = player.GetInventory().GetItemByObjId(stoneUniqueId);
                if (stone == null) return;
                if (!stone.GetItemTemplate().IsStigma() && !PlayerRestrictions.CanUseItem(player, stone)) return;
                Item targetItem = player.GetEquipment().GetEquippedItemByObjId(targetItemUniqueId);
                if (targetItem == null && (targetItem = player.GetInventory().GetItemByObjId(targetItemUniqueId)) == null)
                {
                    SendPacket(action == Action.ENCHANT ? SM_SYSTEM_MESSAGE.STR_ENCHANT_ITEM_NO_TARGET_ITEM() : SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_OPTION_NO_TARGET_ITEM());
                    return;
                }
                if (stone.GetItemTemplate().IsStigma() && targetItem.GetItemTemplate().IsStigma())
                    StigmaService.ChargeStigma(player, targetItem, stone);
                else
                {
                    ItemActions stoneActions = stone.GetItemTemplate().GetActions();
                    EnchantItemAction? enchantAction = stoneActions == null ? null : stoneActions.GetEnchantAction();
                    if (enchantAction == null)
                    {
                        SendPacket(SM_SYSTEM_MESSAGE.STR_ITEM_IS_NOT_USABLE()); return;
                    }
                    if (enchantAction.CanAct(player, stone, targetItem))
                    {
                        Item? supplement = null;
                        if (supplementUniqueId != 0)
                        {
                            supplement = player.GetInventory().GetItemByObjId(supplementUniqueId);
                            if (supplement == null || supplement.GetItemId() / 100000 != 1661)
                            {
                                SendPacket(SM_SYSTEM_MESSAGE.STR_ITEM_ENCHANT_ASSISTANT_CANNOT_FIND()); return;
                            }
                        }
                        enchantAction.Act(player, stone, targetItem, supplement!, targetFusedSlot);
                    }
                }
                break;
            }
            case Action.REMOVE_MANASTONE:
                if (player.GetTarget() is Npc npc && npc.GetObjectId() == npcObjId && PositionUtil.IsInTalkRange(player, npc))
                    ItemSocketService.RemoveManastone(player, targetItemUniqueId, slotNum, targetFusedSlot != 1);
                break;
            case Action.SOCKET_GODSTONE:
            {
                Item weaponItem = player.GetInventory().GetItemByObjId(targetItemUniqueId);
                if (weaponItem == null)
                {
                    bool isEquipped = player.GetEquipment().GetEquippedItemByObjId(targetItemUniqueId) != null;
                    SendPacket(isEquipped ? SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_PROC_CANNOT_GIVE_PROC_TO_EQUIPPED_ITEM() : SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_PROC_NO_TARGET_ITEM()); return;
                }
                ItemSocketService.SocketGodstone(player, weaponItem, stoneUniqueId); break;
            }
            case Action.AMPLIFY:
                EnchantService.AmplifyItem(player, targetItemUniqueId, supplementUniqueId, stoneUniqueId); break;
        }
    }
    private enum Action {ENCHANT = 1, SOCKET_MANASTONE = 2, REMOVE_MANASTONE = 3, SOCKET_GODSTONE = 4, AMPLIFY = 8}
}
