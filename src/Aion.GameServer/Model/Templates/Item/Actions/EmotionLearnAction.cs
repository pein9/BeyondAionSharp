using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/EmotionLearnAction.</summary>
[XmlType("EmotionLearnAction")]
public class EmotionLearnAction : AbstractItemAction
{
    [XmlAttribute("emotionid")] public int emotionId;
    [XmlAttribute("minutes")] public int minutes;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        if (emotionId == 0 || parentItem == null)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_ITEM_COLOR_ERROR());
            return false;
        }
        if (player.GetEmotions() != null && player.GetEmotions().Contains(emotionId))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_SOCIALACTION_ALREADY_HAS_SKILL());
            return false;
        }
        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        Aion.GameServer.Model.Templates.Items.ItemTemplate itemTemplate = parentItem.GetItemTemplate();
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), itemTemplate.GetTemplateId()), true);

        player.GetEmotions().Add(emotionId, minutes == 0 ? 0 : (int)SystemClock.CurrentSeconds() + minutes * 60, true);
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_USE_ITEM(parentItem.GetL10n()));
        player.GetInventory().Delete(parentItem);
    }

    public int GetEmotionId()
    {
        return emotionId;
    }
}
