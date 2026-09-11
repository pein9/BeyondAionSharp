using System.Collections.Generic;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Handlers.PlayerCommands;

/// <summary>Java parity: data/handlers/playercommands/Easter (Neon, Estrayl, Farlon). Exchanges event eggs for prizes.</summary>
public class Easter : PlayerCommand
{
    private static readonly ILogger log = NullLogger.Instance;
    private static readonly int neededItem = 186000175;
    private static readonly IReadOnlyList<Reward> rewards =
    [
        new Reward(50, 186000147, 2), // Mithril Medal
        new Reward(50, 186000055, 3), // Major Ancient Goblet
        new Reward(75, 166020000, 5), // Omega Enchantment Stone
        new Reward(75, 188053609, 3), // [Event] Level 60 Composite Manastone Bundle
        new Reward(75, 166200013, 1), // Enduring Mythic Weapon Tuning Scroll
        new Reward(75, 188053113, 3), // Ahserion's Flight Ancient Manastone Bundle
        new Reward(100, 188053295, 1), // Empyrean Plume Chest
        new Reward(100, 166030005, 5), // Tempering Solution
        new Reward(300, 188053702, 1) // Vasharti's Equipment Box
    ];
    private static readonly IReadOnlyList<Reward> randomRewards =
    [
        new Reward(25, 162002030, 10), // [Event] Premium Restoration Serum
        new Reward(25, 186000237, 50), // Ancient Coin
        new Reward(25, 162000137, 3), // Sublime Life Serum
        new Reward(25, 162000139, 3), // Sublime Mana Serum
        new Reward(25, 186000146, 5), // Guestpetal
        new Reward(25, 188054198, 1), // Greater Scroll Bundle
        new Reward(25, 164000126, 10), // Major Strike Resist Scroll
        new Reward(25, 164000130, 10) // Major Spell Resist Scroll
    ];

    public Easter()
        : base("easter", "Exchanges " + ChatUtil.Item(186000175) + " for prizes.", BuildSyntaxInfo())
    {
    }

    private static string BuildSyntaxInfo()
    {
        string syntaxInfo = "Type in .easter <ID> to get your reward:";
        int i = 1;
        syntaxInfo += "\n[" + i++ + "] - (" + randomRewards[0].RequiredEggs + " eggs) Random item";
        foreach (Reward r in rewards)
            syntaxInfo += "\n[" + i++ + "] - (" + r.RequiredEggs + " eggs) " + r.ItemCount + "x " + ChatUtil.Item(r.ItemId);
        return syntaxInfo;
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(player);
            return;
        }
        int rewardIndex = ParseInt(paramsArr[0]) - 1;
        if (rewardIndex < 0 || rewardIndex >= rewards.Count + 1)
        {
            SendInfo(player, "Invalid reward ID.");
            return;
        }
        Reward reward = rewardIndex == 0 ? Rnd.Get(randomRewards)! : rewards[rewardIndex - 1];
        int cost = reward.RequiredEggs;
        if (player.GetInventory().GetItemCountByItemId(neededItem) < cost || !player.GetInventory().DecreaseByItemId(neededItem, cost))
        {
            SendInfo(player, "You need " + cost + " " + ChatUtil.Item(neededItem) + " for this.");
            return;
        }
        long notAddedCount = ItemService.AddItem(player, reward.ItemId, reward.ItemCount, true,
            new ItemService.ItemUpdatePredicate(ItemPacketService.ItemAddType.DECOMPOSABLE, ItemPacketService.ItemUpdateType.INC_CASH_ITEM));
        if (notAddedCount > 0)
            log.LogWarning("[Easter Event] " + notAddedCount + "/" + reward.ItemCount + " of " + reward.ItemId + " could not be added.");
    }

    private record Reward(int RequiredEggs, int ItemId, long ItemCount);
}
