using Aion.Bots.World;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Items.Enums;

namespace Aion.Bots.Scenarios;

/// <summary>A power shard as its tooltip shows it: the damage a hit gains, and the level its restrict row asks.</summary>
public sealed record NaturalShardInfo(int ItemId, int Boost, int RequiredLevel);

/// <summary>One stack to put into a shard slot.</summary>
public sealed record NaturalShardEquip(int ObjectId, int ItemId, ushort Slot);

/// <param name="BestItemId">The best shard the character owns and may use; null when it owns none.</param>
/// <param name="Equipped">A shard slot holds a stack once <paramref name="Equips"/> are done.</param>
/// <param name="TurnOn">The mode is off and a slot holds a stack: switch it on.</param>
public sealed record NaturalShardPlan(int? BestItemId, IReadOnlyList<NaturalShardEquip> Equips, bool Equipped, bool TurnOn);

/// <summary>
/// The operator, 2026-10-12 (docs/natural-muster.md, rule MR-e): "they should use the best power shard possible (again
/// like the other consumables) and we need to activate them at all times (when logging in, after death, when running
/// out, etc)". Pure: the caller supplies the client-observed inventory and whether the mode is on.
/// A shard is worn like gear but used up like a consumable. The server gates it by its restrict row, as it does a
/// potion, and no shard has one, so the best is the one with the most damage. Java PlayerGameStats.getPowerShardDamage:
/// the right slot's shard is used by the main hand, the left slot's by the off hand, and a two-hand weapon uses both
/// at every hit. So both slots are filled, for every class. Java Equipment.usePowerShard takes the next stack of the
/// same shard from the cube when a slot's stack is used up, and ends the mode when there is none.
/// </summary>
public static class NaturalPowerShardPolicy
{
	/// <summary>ItemSlot.POWER_SHARD_RIGHT and POWER_SHARD_LEFT.</summary>
	public const ushort RightSlot = 1 << 13, LeftSlot = 1 << 14;
	public static readonly ushort[] Slots = [RightSlot, LeftSlot];

	public const string EquipDiagnostic = "power-shard-equip", OnDiagnostic = "power-shard-on";

	/// <summary>The tooltip of a shard for this class, from the shipped item data; null for anything that is no shard.</summary>
	public static Func<int, NaturalShardInfo?> Describer(StaticData data, PlayerClass playerClass) => itemId =>
		data.ItemDataDh.GetItemTemplate(itemId) is { } template && template.GetItemGroup() == ItemGroup.POWER_SHARDS
			? new NaturalShardInfo(itemId, template.GetWeaponBoost(), template.GetRequiredLevel(playerClass)) : null;

	public static bool Worn(BotInventoryItem item) => (item.Details.EquippedSlot.GetValueOrDefault() & (RightSlot | LeftSlot)) != 0;

	public static NaturalShardPlan Decide(IEnumerable<BotInventoryItem> inventory, int level, bool modeOn, Func<int, NaturalShardInfo?> describe)
	{
		(BotInventoryItem Item, NaturalShardInfo Info)[] shards = [.. inventory.Where(item => item.Count > 0)
			.Select(item => (item, info: describe(item.ItemId))).Where(pair => pair.info != null).Select(pair => (pair.item, pair.info!))];
		NaturalShardInfo? best = shards.Where(shard => shard.Info.RequiredLevel >= 1 && shard.Info.RequiredLevel <= level)
			.Select(shard => shard.Info).OrderByDescending(info => info.Boost).ThenBy(info => info.ItemId).FirstOrDefault();
		var equips = new List<NaturalShardEquip>();
		bool equipped = shards.Any(shard => Worn(shard.Item));
		if (best != null)
		{
			var free = new Queue<BotInventoryItem>(shards.Where(shard => shard.Info.ItemId == best.ItemId && !Worn(shard.Item))
				.Select(shard => shard.Item).OrderBy(item => item.ObjectId));
			foreach (ushort slot in Slots)
			{
				BotInventoryItem? worn = shards.Select(shard => shard.Item).FirstOrDefault(item => (item.Details.EquippedSlot.GetValueOrDefault() & slot) != 0);
				if (worn?.ItemId == best.ItemId || free.Count == 0) continue;
				BotInventoryItem stack = free.Dequeue();
				equips.Add(new(stack.ObjectId, stack.ItemId, slot));
				equipped = true;
			}
		}
		return new(best?.ItemId, equips, equipped, equipped && !modeOn);
	}
}
