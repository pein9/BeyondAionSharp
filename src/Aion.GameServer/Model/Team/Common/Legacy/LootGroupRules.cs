using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Model.Actions;
using Aion.GameServer.Model.Drop;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Utils;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Model.Team.Common.Legacy;

/// <summary>
/// Java parity: model/team/common/legacy/LootGroupRules (ATracer, xTz).
/// Java ConcurrentLinkedDeque → LinkedList (closest Deque with contains/remove; concurrency is a foundational diff).
/// </summary>
public class LootGroupRules
{
    private readonly LootRuleType lootRule;
    public const int SEVENTH_GRADE_RULE = 2;
    private readonly bool autoDistributeJunk;
    private readonly int commonItemAbove;
    private readonly int superiorItemAbove;
    private readonly int heroicItemAbove;
    private readonly int fabledItemAbove;
    private readonly int eternalItemAbove;
    private readonly int mythicItemAbove;
    private int junkRecipientIndex;
    private int nrRoundRobin;
    private readonly LinkedList<DropItem> itemsToBeDistributed = new LinkedList<DropItem>();

    public LootGroupRules()
    {
        lootRule = LootRuleType.ROUNDROBIN;
        autoDistributeJunk = false;
        commonItemAbove = 0;
        superiorItemAbove = 2;
        heroicItemAbove = 2;
        fabledItemAbove = 2;
        eternalItemAbove = 2;
        mythicItemAbove = 2;
    }

    public LootGroupRules(LootRuleType lootRule, bool autoDistributeJunk, int commonItemAbove, int superiorItemAbove, int heroicItemAbove, int fabledItemAbove,
        int eternalItemAbove, int mythicItemAbove)
    {
        this.lootRule = lootRule;
        this.autoDistributeJunk = autoDistributeJunk;
        this.commonItemAbove = commonItemAbove;
        this.superiorItemAbove = superiorItemAbove;
        this.heroicItemAbove = heroicItemAbove;
        this.fabledItemAbove = fabledItemAbove;
        this.eternalItemAbove = eternalItemAbove;
        this.mythicItemAbove = mythicItemAbove;
    }

    public bool GetQualityRule(ItemQuality quality)
    {
        return quality switch
        {
            ItemQuality.COMMON => commonItemAbove != 0, // White
            ItemQuality.RARE => superiorItemAbove != 0, // Green
            ItemQuality.LEGEND => heroicItemAbove != 0, // Blue
            ItemQuality.UNIQUE => fabledItemAbove != 0, // Yellow
            ItemQuality.EPIC => eternalItemAbove != 0, // Orange
            ItemQuality.MYTHIC => mythicItemAbove != 0, // Purple
            _ => false,
        };
    }

    public bool IsAutoDistributedJunk(ItemQuality? quality)
    {
        return autoDistributeJunk && quality == ItemQuality.JUNK;
    }

    public bool IsAutoDistributeJunk() => autoDistributeJunk;

    public int GetJunkDistributionChangeMessageId(LootGroupRules? previousRules)
    {
        if (previousRules != null && previousRules.autoDistributeJunk == autoDistributeJunk)
            return 0;
        return (autoDistributeJunk ? SM_SYSTEM_MESSAGE.STR_MSG_JUNK_DISTRIBUTE_ON() : SM_SYSTEM_MESSAGE.STR_MSG_JUNK_DISTRIBUTE_OF()).GetId();
    }

    public LootRuleType GetLootRule()
    {
        return lootRule;
    }

    public int GetAutodistributionId()
    {
        bool isBid = mythicItemAbove == 3;
        bool isRoll = mythicItemAbove == 2;
        return isBid ? 3 : isRoll ? 2 : 0;
    }

    public int GetCommonItemAbove()
    {
        return commonItemAbove;
    }

    public int GetSuperiorItemAbove()
    {
        return superiorItemAbove;
    }

    public int GetHeroicItemAbove()
    {
        return heroicItemAbove;
    }

    public int GetFabledItemAbove()
    {
        return fabledItemAbove;
    }

    public int GetEternalItemAbove()
    {
        return eternalItemAbove;
    }

    public int GetMythicItemAbove()
    {
        return mythicItemAbove;
    }

    public int GetJunkRecipientIndex()
    {
        return junkRecipientIndex;
    }

    public void SetJunkRecipientIndex(int junkRecipientIndex)
    {
        this.junkRecipientIndex = junkRecipientIndex;
    }

    public void SetPlayersInRoll(ICollection<Aion.GameServer.Model.GameObjects.Players.Player> players, int time, int index, int npcId)
    {
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            foreach (Aion.GameServer.Model.GameObjects.Players.Player player in players)
            {
                if (player.IsInPlayerMode(PlayerMode.IN_ROLL))
                {
                    Aion.GameServer.Model.GameObjects.Players.InRoll inRoll = player.inRoll;
                    if (inRoll.GetIndex() == index && inRoll.GetNpcId() == npcId)
                        Aion.GameServer.Services.Drop.DropDistributionService.GetInstance().HandleRollOrBid(player, inRoll.GetRollType(), 0, 0, inRoll.GetItemId(), inRoll.GetNpcId(),
                            inRoll.GetIndex());
                }
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(time));
    }

    public int GetNrRoundRobin()
    {
        return nrRoundRobin;
    }

    public void SetNrRoundRobin(int nrRoundRobin)
    {
        this.nrRoundRobin = nrRoundRobin;
    }

    public void AddItemToBeDistributed(DropItem dropItem)
    {
        itemsToBeDistributed.AddLast(dropItem);
    }

    public bool ContainDropItem(DropItem dropItem)
    {
        return itemsToBeDistributed.Contains(dropItem);
    }

    public void RemoveItemToBeDistributed(DropItem dropItem)
    {
        itemsToBeDistributed.Remove(dropItem);
    }

    public LinkedList<DropItem> GetItemsToBeDistributed()
    {
        return itemsToBeDistributed;
    }
}
