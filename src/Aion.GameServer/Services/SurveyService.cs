using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Cache;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Survey;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/SurveyService (KID).</summary>
public class SurveyService
{
    private static readonly ILogger log = AionLog.For(nameof(SurveyService));
    // Java parity: LinkedHashMap (insertion-ordered) — Dictionary preserves insertion order until removal.
    private readonly Dictionary<int, SurveyItem> activeItems = new Dictionary<int, SurveyItem>();

    private SurveyService()
    {
        ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct => { TaskUpdateImpl(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(2000), TimeSpan.FromMilliseconds(SecurityConfig.SURVEY_DELAY * 60000));
        log.LogInformation("SurveyService initialized");
    }

    public bool IsActive(Player player, int survId)
    {
        bool avail = activeItems.ContainsKey(survId);
        if (avail)
            RequestSurvey(player, survId);

        return avail;
    }

    public void RequestSurvey(Player player, int survId)
    {
        activeItems.TryGetValue(survId, out SurveyItem item);
        if (item == null || item.ownerId != player.GetObjectId())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_FIND_POLL());
            return;
        }

        ItemTemplate template = DataManager.ITEM_DATA.GetItemTemplate(item.itemId);
        if (template == null)
        {
            return;
        }
        if (player.GetInventory().IsFull(template.GetExtraInventoryId()))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_FULL_INVENTORY());
            log.LogWarning("[SurveyController] player {PlayerName} tried to receive item with full inventory", player.GetName());
            return;
        }
        if (SurveyControllerDAO.UseItem(item.uniqueId))
        {
            ItemService.AddItem(player, item.itemId, item.count);
            if (item.itemId == ItemId.KINAH)
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_GET_POLL_REWARD_MONEY(item.count));
            else if (item.count == 1)
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_GET_POLL_REWARD_ITEM(template.GetL10n()));
            else
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_GET_POLL_REWARD_ITEM_MULTI(item.count, template.GetL10n()));

            activeItems.Remove(survId);
        }
    }

    public void TaskUpdateImpl()
    {
        List<SurveyItem> newList = SurveyControllerDAO.GetAllUnused();
        if (newList.Count == 0)
            return;

        List<int> players = new List<int>();
        int cnt = 0;
        foreach (SurveyItem survey in newList)
        {
            if (activeItems.TryAdd(survey.uniqueId, survey))
            {
                cnt++;
                if (!players.Contains(survey.ownerId))
                    players.Add(survey.ownerId);
            }
        }
        log.LogInformation("[SurveyController] found new {Items} items for {Players} players", cnt, players.Count);
        foreach (int ownerId in players)
        {
            Player player = Aion.GameServer.World.World.GetInstance().GetPlayer(ownerId);
            if (player != null)
            {
                ShowAvailable(player);
            }
        }
    }

    public void ShowAvailable(Player player)
    {
        foreach (SurveyItem item in this.activeItems.Values)
        {
            if (item.ownerId != player.GetObjectId())
                continue;

            string context = HTMLCache.GetInstance().GetHTML("surveyTemplate.xhtml");
            context = context.Replace("%itemid%", item.itemId + "");
            context = context.Replace("%itemcount%", item.count + "");
            context = context.Replace("%html%", item.html);
            context = context.Replace("%radio%", item.radio);

            HTMLService.SendData(player, item.uniqueId, context);
        }
    }

    private static class SingletonHolder
    {
        internal static readonly SurveyService instance = new SurveyService();
    }

    public static SurveyService GetInstance()
    {
        return SingletonHolder.instance;
    }
}
