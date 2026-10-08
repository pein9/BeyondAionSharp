using Aion.Bots.Protocol;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.Bots.Scenarios;

/// <summary>What one container gave when it was opened: the items whose owned count rose.</summary>
public sealed record NaturalOpenedContainer(int ItemId, int ObjectId, IReadOnlyDictionary<int, long> Gained);

/// <summary>One inventory check, as the trace records it.</summary>
public sealed record NaturalInventoryCheckResult(string Trigger, NaturalGearUpgrade[] Worn, NaturalOpenedContainer[] Opened,
	NaturalJourneyItem[] Discarded, int[] NotOpened, int? FreeSlots);

/// <summary>
/// AX-04: the inventory management check the operator asked for after every quest turn-in (2026-10-06): wear better
/// gear, open the reward containers the leg names, discard what it says to discard, and count the cube's free slots.
/// Everything is an ordinary client action decided from the observed inventory; selling waits for a vendor visit.
/// </summary>
public static class NaturalInventoryCheck
{
	public const string Diagnostic = "inventory-check";

	/// <summary>The client's tooltip view of an item for this class and race (the natural character is created male).</summary>
	public static NaturalGearInfo? Describe(ItemTemplate? template, PlayerClass playerClass, Race race)
	{
		if (template == null || template.GetItemSlot() == 0) return null;
		var genderLimit = template.GetUseLimits()?.GetGenderPermitted();
		return new NaturalGearInfo(template.GetItemSlot(), template.GetRequiredLevel(playerClass), template.GetLevel(),
			(template.GetRace() == Race.PC_ALL || template.GetRace() == race) && (genderLimit == null || genderLimit == Gender.MALE),
			template.GetItemGroup().ToString(), template.GetWeaponStats()?.GetBoostMagicalSkill() ?? 0,
			// CP-29: what a table rule's physical stat reads; the flat physical-attack lines of the tooltip, without conditions.
			template.GetWeaponStats()?.GetMinDamage() ?? 0, template.GetWeaponStats()?.GetMaxDamage() ?? 0,
			template.GetModifiers()?.Where(modifier => modifier.GetName() == Aion.GameServer.Model.Stats.Container.StatEnum.PHYSICAL_ATTACK &&
				modifier.GetType() == typeof(Aion.GameServer.Model.Stats.Calc.Functions.StatAddFunction) && !modifier.HasConditions())
				.Sum(modifier => modifier.GetValue()) ?? 0,
			template.IsOneHandWeapon());
	}

	/// <summary>Wear what <see cref="NaturalGearPolicy.SelectUpgrades"/> picks from <paramref name="wearable"/>. The server
	/// still checks every equip; an item it refuses joins <paramref name="refused"/> and is never asked for again.</summary>
	/// <param name="rules">CP-23: the class's gear rules, for the hand rule; the Priest line's when not given.</param>
	public static async Task<IReadOnlyList<NaturalGearUpgrade>> EquipAsync(INaturalJourneySession session, IEnumerable<BotInventoryItem> wearable,
		Func<int, NaturalGearInfo?> describe, long offHandSlots, HashSet<int> refused, CancellationToken token, NaturalGearRules? rules = null)
	{
		BotWorldModel world = session.Api.World;
		var worn = new List<NaturalGearUpgrade>();
		if (world.IsDead) return worn;
		bool dualWield = world.Skills.Keys.Any(NaturalGearPolicy.DualWieldSkillIds.Contains);
		foreach (NaturalGearUpgrade upgrade in NaturalGearPolicy.SelectUpgrades(wearable, world.Level, describe, offHandSlots, refused, rules, dualWield))
		{
			await session.SendPacketAsync(session.Api.Equip(0, upgrade.Slot, upgrade.ObjectId), token);
			await session.SynchronizeAsync(token);
			bool isWorn = world.Inventory.TryGetValue(upgrade.ObjectId, out BotInventoryItem? after) && (after.Details.EquippedSlot ?? 0) > 0;
			if (isWorn) worn.Add(upgrade);
			else refused.Add(upgrade.ObjectId);
			session.TraceDiagnostic("gear-equip", new Dictionary<string, object?>
			{
				["itemId"] = upgrade.ItemId,
				["objectId"] = upgrade.ObjectId,
				["slot"] = upgrade.Slot,
				["itemLevel"] = upgrade.ItemLevel,
				["replacesItemLevel"] = upgrade.ReplacesItemLevel,
				["worn"] = isWorn,
			});
		}
		return worn;
	}

	/// <summary>Open every owned container in <paramref name="containerIds"/>, one use at a time: the use bar, then the
	/// item's own use delay before the next. A container the server does not open (a full cube) is reported, not retried.</summary>
	public static async Task<(IReadOnlyList<NaturalOpenedContainer> Opened, IReadOnlyList<int> NotOpened)> OpenAsync(INaturalJourneySession session,
		IEnumerable<int> containerIds, Func<int, ItemTemplate?> templates, CancellationToken token)
	{
		BotWorldModel world = session.Api.World;
		var opened = new List<NaturalOpenedContainer>();
		var notOpened = new List<int>();
		foreach (int containerId in containerIds)
		{
			ItemTemplate template = templates(containerId) ?? throw new InvalidDataException($"Container {containerId} has no item template.");
			while (!world.IsDead && world.Inventory.Values.FirstOrDefault(item => item.ItemId == containerId) is { } container)
			{
				Dictionary<int, long> before = Counts(world);
				await session.SendPacketAsync(session.Api.UseItem(container.ObjectId, template), token);
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(template.GetCastingDelay() + 100), token);
				await session.SynchronizeAsync(token);
				Dictionary<int, long> after = Counts(world);
				if (after.GetValueOrDefault(containerId) >= before[containerId])
				{
					notOpened.Add(containerId);
					break;
				}
				var gained = after.Where(item => item.Key != containerId && item.Value > before.GetValueOrDefault(item.Key))
					.ToDictionary(item => item.Key, item => item.Value - before.GetValueOrDefault(item.Key));
				opened.Add(new NaturalOpenedContainer(containerId, container.ObjectId, gained));
				session.TraceDiagnostic("reward-container-opened", new Dictionary<string, object?>
				{
					["itemId"] = containerId, ["objectId"] = container.ObjectId, ["gained"] = gained,
				});
				// The same container, or another in its delay group, cannot be used again before the delay runs out.
				await session.AdvanceAsync(TimeSpan.FromMilliseconds((template.GetUseLimits()?.GetDelayTime() ?? 0) + 100), token);
				await session.SynchronizeAsync(token);
			}
		}
		return (opened, notOpened);
	}

	/// <summary>Discard every owned stack of <paramref name="itemIds"/> (the client's drag to the trash and its confirmation).</summary>
	public static async Task<IReadOnlyList<NaturalJourneyItem>> DiscardAsync(INaturalJourneySession session, IEnumerable<int> itemIds,
		CancellationToken token)
	{
		BotWorldModel world = session.Api.World;
		var discarded = new List<NaturalJourneyItem>();
		foreach (int itemId in itemIds)
			foreach (BotInventoryItem item in world.Inventory.Values.Where(item => item.ItemId == itemId).ToArray())
			{
				await session.SendPacketAsync(GameClientPackets.DeleteItem(item.ObjectId), token);
				await session.SynchronizeAsync(token);
				if (world.Inventory.ContainsKey(item.ObjectId))
					throw new InvalidDataException($"The server kept item {itemId} that the leg discards.");
				var gone = new NaturalJourneyItem(item.ObjectId, item.ItemId, item.Count, item.EquipmentSlot);
				discarded.Add(gone);
				session.TraceDiagnostic("item-discarded", new Dictionary<string, object?>
				{
					["itemId"] = gone.ItemId, ["objectId"] = gone.ObjectId, ["count"] = gone.Count,
				});
			}
		return discarded;
	}

	/// <summary>The whole check, in the operator's order: wear, open, discard, count. What the leg keeps sealed must still
	/// be owned afterwards.</summary>
	/// <param name="equipAsync">The journey's own equipment check, which knows what an open quest still needs.</param>
	public static async Task<NaturalInventoryCheckResult> RunAsync(INaturalJourneySession session, string trigger, NaturalAbyssInventory rules,
		Func<CancellationToken, Task<IReadOnlyList<NaturalGearUpgrade>>> equipAsync, Func<int, ItemTemplate?> templates, Func<int?> freeSlots,
		CancellationToken token)
	{
		Dictionary<int, long> sealedBefore = rules.KeepSealed.ToDictionary(id => id, id => Counts(session.Api.World).GetValueOrDefault(id));
		IReadOnlyList<NaturalGearUpgrade> worn = await equipAsync(token);
		var (opened, notOpened) = await OpenAsync(session, rules.Open, templates, token);
		IReadOnlyList<NaturalJourneyItem> discarded = await DiscardAsync(session, rules.Discard, token);
		// A container can hold gear: wear what it gave.
		if (opened.Count > 0) worn = [.. worn, .. await equipAsync(token)];
		Dictionary<int, long> counts = Counts(session.Api.World);
		foreach ((int id, long count) in sealedBefore)
			if (counts.GetValueOrDefault(id) != count) throw new InvalidDataException($"Sealed item {id} changed during the inventory check.");
		var result = new NaturalInventoryCheckResult(trigger, [.. worn], [.. opened], [.. discarded], [.. notOpened], freeSlots());
		session.TraceDiagnostic(Diagnostic, new Dictionary<string, object?>
		{
			["trigger"] = trigger, ["worn"] = result.Worn.Select(upgrade => upgrade.ItemId).ToArray(),
			["opened"] = result.Opened.Select(container => container.ItemId).ToArray(),
			["discarded"] = result.Discarded.Select(item => item.ItemId).ToArray(), ["notOpened"] = result.NotOpened, ["freeSlots"] = result.FreeSlots,
		});
		return result;
	}

	private static Dictionary<int, long> Counts(BotWorldModel world) =>
		world.Inventory.Values.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
}
