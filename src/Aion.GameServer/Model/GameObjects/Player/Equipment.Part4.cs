using Aion.GameServer.Utils.Stats;
using System;
using System.Threading;
using System.Threading.Tasks;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Items;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>
/// Java parity: model/gameobjects/player/Equipment — partial #4 (Java ~699-802): soulBindItem (with
/// RequestResponseHandler/ActionObserver anonymous classes → private nested named classes), rank-limit checks.
/// </summary>
public partial class Equipment
{
    private bool SoulBindItem(Player player, Item item, long slot)
    {
        if (player.GetInventory().GetItemByObjId(item.GetObjectId()) == null)
            return false;
        if (player.IsInState(CreatureState.WEAPON_EQUIPPED) || !CreatureStateExtensions.IsStanding(player.GetState()))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_SOUL_BOUND_INVALID_STANCE(CreatureStateExtensions.GetActionState(player.GetState()).GetL10n()));
            return false;
        }

        Aion.GameServer.Model.GameObjects.Players.RequestResponseHandler<Player> responseHandler = new SoulBindResponseHandler(player, this, item, slot);

        bool requested = player.GetResponseRequester().PutRequest(Aion.GameServer.Network.Aion.ServerPackets.SM_QUESTION_WINDOW.STR_SOUL_BOUND_ITEM_DO_YOU_WANT_SOUL_BOUND, responseHandler);
        if (requested)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SM_QUESTION_WINDOW(Aion.GameServer.Network.Aion.ServerPackets.SM_QUESTION_WINDOW.STR_SOUL_BOUND_ITEM_DO_YOU_WANT_SOUL_BOUND, 0, 0, item.GetL10n()));
        }
        else
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_SOUL_BOUND_CLOSE_OTHER_MSG_BOX_AND_RETRY());
        }
        return false;
    }

    // Java parity: anonymous RequestResponseHandler<Player> in soulBindItem.
    private sealed class SoulBindResponseHandler : Aion.GameServer.Model.GameObjects.Players.RequestResponseHandler<Player>
    {
        private readonly Equipment eq;
        private readonly Item item;
        private readonly long slot;

        public SoulBindResponseHandler(Player player, Equipment eq, Item item, long slot) : base(player)
        {
            this.eq = eq;
            this.item = item;
            this.slot = slot;
        }

        public override void AcceptRequest(Player requester, Player responder)
        {
            responder.GetController().CancelUseItem();

            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(responder,
                new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(responder.GetObjectId(), item.GetObjectId(), item.GetItemId(), 5000, ItemUseAnimation.SOUL_BIND_START), true);

            ActionObserver observer = new SoulBindItemUseObserver(responder, item);
            responder.GetObserveController().AddObserver(observer);

            // item usage animation
            responder.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                responder.GetObserveController().RemoveObserver(observer);

                Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(responder,
                    new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(responder.GetObjectId(), item.GetObjectId(), item.GetItemId(), 0, ItemUseAnimation.SOUL_BIND_SUCCESS), true);
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(responder, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_SOUL_BOUND_ITEM_SUCCEED(item.GetL10n()));

                item.SetSoulBound(true);
                Aion.GameServer.Services.Items.ItemPacketService.UpdateItemAfterInfoChange(eq.owner, item);

                eq.Equip(slot, item);
                Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(responder, new Aion.GameServer.Network.Aion.ServerPackets.SM_UPDATE_PLAYER_APPEARANCE(responder.GetObjectId(), eq.GetEquippedForAppearance()), true);
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(5000)));
        }

        public override void DenyRequest(Player requester, Player responder)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(responder, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_SOUL_BOUND_ITEM_CANCELED(item.GetL10n()));
        }
    }

    // Java parity: anonymous ItemUseObserver in soulBindItem's acceptRequest.
    private sealed class SoulBindItemUseObserver : Aion.GameServer.Controllers.Observer.ItemUseObserver
    {
        private readonly Player responder;
        private readonly Item item;

        public SoulBindItemUseObserver(Player responder, Item item)
            : base(responder)
        {
            this.responder = responder;
            this.item = item;
        }

        protected override void OnAbort()
        {
            responder.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(responder, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_SOUL_BOUND_ITEM_CANCELED(item.GetL10n()));
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(responder,
                new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(responder.GetObjectId(), item.GetObjectId(), item.GetItemId(), 0, ItemUseAnimation.SOUL_BIND_CANCEL), true);
        }
    }

    private bool VerifyRankLimits(Item item)
    {
        return FindUnmetRankLimits(item, owner.GetAbyssRank().GetRank().GetId()) == null;
    }

    /// <summary>The limits of the item or of the item fused into it that the given abyss rank does not satisfy, null if it satisfies both.</summary>
    private Aion.GameServer.Model.Templates.Items.ItemUseLimits FindUnmetRankLimits(Item item, int rank)
    {
        if (!item.GetItemTemplate().GetUseLimits().VerifyRank(rank))
            return item.GetItemTemplate().GetUseLimits();
        if (item.GetFusionedItemTemplate() != null && !item.GetFusionedItemTemplate().GetUseLimits().VerifyRank(rank))
            return item.GetFusionedItemTemplate().GetUseLimits();
        return null;
    }

    /// <summary>
    /// Gives the owner ten minutes to keep wearing the items his abyss rank no longer allows, warns him a minute before the end and takes them off
    /// when the time is up. Regaining the rank in the meantime cancels it. The deadline is stored with the item, so it keeps running across a relog.
    /// Reschedules itself for the earliest warning or removal still ahead.
    /// </summary>
    public void CheckRankLimitItems()
    {
        int now = (int)(Aion.GameServer.Utils.SystemClock.CurrentMillis() / 1000);
        int nextCheck = 0;
        bool appearanceChanged = false;
        foreach (Item item in GetEquippedItems())
        {
            if (!item.IsEquipped()) // unequipping a main hand weapon takes the off hand one off too, so the snapshot can hold items that already left
                continue;
            int expireTime = item.GetRankLimitExpireTime();
            if (VerifyRankLimits(item))
            {
                if (expireTime != 0)
                    item.SetRankLimitExpireTime(0); // the rank came back in time
                continue;
            }
            if (expireTime == 0)
            { // items that are already counting down keep their own deadline
                expireTime = now + RANK_LIMIT_GRACE_SECONDS;
                item.SetRankLimitExpireTime(expireTime);
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_UNEQUIP_RANKITEM_TIMER_10M(item.GetL10n()));
            }
            else if (expireTime <= now)
            {
                item.SetRankLimitExpireTime(0);
                if (UnEquipItem(item.GetObjectId(), false) != null)
                {
                    appearanceChanged = true;
                    Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_UNEQUIP_RANKITEM(item.GetL10n()));
                }
                continue;
            }
            int warningTime = expireTime - RANK_LIMIT_WARNING_SECONDS;
            if (warningTime > lastRankLimitCheck && warningTime <= now) // only the run crossing it warns, so it stays a single message
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_UNEQUIP_RANKITEM_TIMER_1M(item.GetL10n()));
            int itemNextCheck = warningTime > now ? warningTime : expireTime; // both are in the future, so the task can never reschedule itself instantly
            if (nextCheck == 0 || itemNextCheck < nextCheck)
                nextCheck = itemNextCheck;
        }
        if (appearanceChanged) // only the packet handlers do this, so items taken off by the server would stay visible until the next equip change
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SM_UPDATE_PLAYER_APPEARANCE(owner.GetObjectId(), GetEquippedForAppearance()), true);
        lastRankLimitCheck = now;
        if (nextCheck == 0)
            owner.GetController().CancelTask(Aion.GameServer.Model.TaskId.RANK_LIMIT_UNEQUIP);
        else
            owner.GetController().AddTask(Aion.GameServer.Model.TaskId.RANK_LIMIT_UNEQUIP, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                CheckRankLimitItems();
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds((nextCheck - now) * 1000L)));
    }
}
