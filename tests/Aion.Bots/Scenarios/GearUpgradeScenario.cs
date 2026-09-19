using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public interface IGearUpgradeDriver : IGearSocketDriver
{
	Task<int> ApproachServiceAsync(int npcTemplateId, CancellationToken token);
}

/// <summary>G5: real purification payment, skin transfer/reset, tuning proposals and two conditioning levels.</summary>
public static class GearUpgradeScenario
{
	public const int MapId = 110010000, RemodelNpcId = 203731, ConditioningNpcId = 205455;
	public static BotPosition Position { get; } = new(1623.699f, 1510.399f, 573.6294f, 60);
	// Existing shipped purification test recipe deliberately covers its nonzero kinah cost (§7 #47).
	// No template/recipe is added or modified; the input is identified and enchanted by client actions.
	public const int BaseId = 100001440, ResultId = 100001765;
	public const int TunableId = 100001551, ScrollId = 166200009; // Modor's Sword; Mythic Weapon Tuning Scroll.
	public const int SkinId = 100000196, ReshaperId = 168100000, ConditionedId = 100001129;
	public const int EnchantStoneId = 166000195; // Epsilon: suitable for the level-55 purification input; real rolls still apply.
	public static IReadOnlyList<(int Id, int Count)> Grants { get; } = new[]
	{
		(BotWorldModel.KinahItemId, 5_000_000), (BaseId, 1), (100001614, 1), (186000005, 10), (162000016, 100),
		(TunableId, 1), (ScrollId, 2), (SkinId, 1), (ReshaperId, 1), (ConditionedId, 1), (EnchantStoneId, 64)
	};

	public static async Task RunAsync(IGearUpgradeDriver driver, CancellationToken token = default)
	{
		await driver.StepAsync("prepare-unfinished-gear-and-purification-materials", async ct =>
		{
			await driver.PrepareAsync(ct);
			await driver.SynchronizeAsync(ct);
		}, token);
		await IdentifyAsync(BaseId, 3, 5);
		int attempts = 0;
		while (Enchantment(Item(BaseId)).EnchantLevel < 5 && attempts++ < 64)
			await driver.StepAsync($"enchant-purification-input-{attempts:D2}", async ct =>
			{
				var expected = Copy(); var original = Item(BaseId); var stone = Item(EnchantStoneId);
				byte oldLevel = Enchantment(original).EnchantLevel;
				bool success = await GearSocketScenario.UseAsync(driver, driver.Api.EnchantItem(original.ObjectId, stone.ObjectId), stone, 4000, ct);
				byte newLevel = Enchantment(Item(BaseId)).EnchantLevel;
				Require(success ? newLevel > oldLevel && newLevel <= oldLevel + 3 : newLevel == Math.Max(0, oldLevel - 1), "Unexpected purification-input enchant outcome.");
				Consume(expected, stone, 1);
				expected[original.ObjectId] = original with { Details = original.Details with { Enchantment = Enchantment(original) with { EnchantLevel = newLevel } } };
				AssertInventory(expected, "enchant input");
			}, token);
		Require(Enchantment(Item(BaseId)).EnchantLevel >= 5, "Bounded enchantment attempts did not reach the purification prerequisite.");
		await driver.StepAsync("purify-and-pay-exact-material-and-kinah-cost", async ct =>
		{
			var expected = Copy(); var original = Item(BaseId);
			var materials = new[] { Item(100001614), Item(186000005), Item(162000016) };
			await driver.SendAsync(driver.Api.PurifyItem(original.ObjectId, ResultId, materials.Select(item => item.ObjectId).ToArray()), ct);
			await driver.WaitAsync(typeof(SM_INVENTORY_ADD_ITEM), _ => driver.Api.World.Inventory.Values.Any(item => item.ItemId == ResultId), ct);
			await driver.SynchronizeAsync(ct);
			var result = Item(ResultId);
			Require(result.ObjectId != original.ObjectId && result.Count == 1 && result.Creator == original.Creator && result.EquipmentSlot == original.EquipmentSlot,
				"Purification must replace the base with a new inventory item.");
			var details = original.Details with { PackCount = 0, Premium = new(0, 0), Enchantment = Enchantment(original) with
			{
				SkinId = ResultId, EnchantLevel = (byte)(Enchantment(original).EnchantLevel - 5)
			} };
			Require(result.Details == details, "Purification did not preserve the input gear fields and subtract exactly five enchant levels.");
			expected.Remove(original.ObjectId);
			// The new object ID and template metadata come from the wire; the independent storage oracle checks them on relog.
			expected.Add(result.ObjectId, result);
			Consume(expected, materials[0], 1); Consume(expected, materials[1], 10); Consume(expected, materials[2], 100);
			Pay(expected, 1000);
			AssertInventory(expected, "purification");
			await driver.VerifyPersistenceAsync(ct);
		}, token);

		await RemodelAsync(SkinId, SkinId);
		await IdentifyAsync(TunableId, 1, 2);
		await RetuneAsync(accepted: false);
		await driver.StepAsync("persist-remodeled-skin-and-rejected-tuning", driver.VerifyPersistenceAsync, token);
		await RetuneAsync(accepted: true);
		await RemodelAsync(ReshaperId, ResultId);
		for (byte level = 1; level <= 2; level++)
		{
			byte requestedLevel = level;
			await driver.StepAsync($"condition-level-{level}-with-exact-fee", async ct =>
			{
				int npc = await driver.ApproachServiceAsync(ConditioningNpcId, ct);
				await OpenServiceAsync(npc, 75, ct);
				var expected = Copy(); var original = Item(ConditionedId);
				Require(original.Details.ChargePoints == (requestedLevel == 1 ? 0 : 500_000), "Conditioning did not start at the preceding level.");
				await driver.SendAsync(driver.Api.ChargeItems(npc, requestedLevel, original.ObjectId), ct);
				await driver.WaitAsync(typeof(SM_INVENTORY_UPDATE_ITEM), p => p.Get<int>("objectId") == original.ObjectId, ct);
				await driver.SynchronizeAsync(ct);
				expected[original.ObjectId] = original with { Details = original.Details with { ChargePoints = requestedLevel * 500_000 } };
				Pay(expected, requestedLevel == 1 ? 360_000 : 720_000); // Shipped price1=720000, price2=1440000; no vendor multiplier.
				AssertInventory(expected, "conditioning");
				await driver.SendAsync(driver.Api.CloseDialog(npc), ct);
			}, token);
		}
		await driver.StepAsync("persist-purification-restored-skin-accepted-tuning-and-conditioning", driver.VerifyPersistenceAsync, token);

		async Task IdentifyAsync(int itemId, int maxSockets, int maxEnchantBonus)
		{
			await driver.StepAsync($"identify-item-{itemId}-with-real-cast", async ct =>
			{
				var expected = Copy(); var original = Item(itemId);
				Require(original.Details.Premium is { BonusStatsId: 255, TuneCount: 0 }, "Identification input is already identified.");
				await UseAsync(original.ObjectId, original.ObjectId, 0, 9, 10, ct);
				await driver.WaitAsync(typeof(SM_INVENTORY_UPDATE_ITEM), p => p.Get<int>("objectId") == original.ObjectId, ct);
				await driver.SynchronizeAsync(ct);
				var identified = Item(itemId); var enchantment = Enchantment(identified);
				Require(enchantment.OptionalSockets <= maxSockets && enchantment.EnchantBonus <= maxEnchantBonus && identified.Details.Premium is { BonusStatsId: < 255, TuneCount: 0 },
					"Identification produced fields outside the shipped template bounds.");
				expected[original.ObjectId] = original with { Details = original.Details with
				{
					Premium = identified.Details.Premium,
					Enchantment = Enchantment(original) with { OptionalSockets = enchantment.OptionalSockets, EnchantBonus = enchantment.EnchantBonus }
				} };
				AssertInventory(expected, "identify");
			}, token);
		}
		async Task RetuneAsync(bool accepted)
		{
			await driver.StepAsync(accepted ? "accept-tuning-proposal" : "reject-tuning-proposal", async ct =>
			{
				var expected = Copy(); var original = Item(TunableId); var scroll = Item(ScrollId);
				await UseAsync(scroll.ObjectId, original.ObjectId, scroll.ObjectId, 12, 13, ct);
				var proposal = await driver.WaitAsync(typeof(SM_TUNE_RESULT), p => p.Get<int>("objectId") == original.ObjectId, ct);
				Require(proposal.Get<int>("tuningScrollItemId") == ScrollId && proposal.Get<bool>("tuneCancelPossible") && proposal.Get<bool>("showManastoneSlots"), "Wrong tuning proposal mode.");
				var rolled = proposal.Get<BotItemEnchantment>("enchantment"); byte bonus = proposal.Get<byte>("statBonusId");
				Require(rolled.OptionalSockets <= 1 && rolled.EnchantBonus <= 2 && bonus < 255, "Retune proposal exceeds template bounds.");
				await driver.SynchronizeAsync(ct);
				Consume(expected, scroll, 1);
				AssertInventory(expected, "pending proposal must not replace item stats before a choice");
				await driver.SendAsync(driver.Api.TuneResult(original.ObjectId, accepted), ct);
				await driver.WaitAsync(typeof(SM_INVENTORY_UPDATE_ITEM), p => p.Get<int>("objectId") == original.ObjectId, ct);
				await driver.SynchronizeAsync(ct);
				expected[original.ObjectId] = original with { Details = original.Details with
				{
					Premium = new(accepted ? bonus : original.Details.Premium!.BonusStatsId, (byte)(original.Details.Premium!.TuneCount + 1)),
					Enchantment = accepted ? Enchantment(original) with { OptionalSockets = rolled.OptionalSockets, EnchantBonus = rolled.EnchantBonus } : Enchantment(original)
				} };
				AssertInventory(expected, "tuning decision");
			}, token);
		}
		async Task RemodelAsync(int materialId, int newSkinId)
		{
			await driver.StepAsync($"remodel-using-{materialId}", async ct =>
			{
				int npc = await driver.ApproachServiceAsync(RemodelNpcId, ct);
				await OpenServiceAsync(npc, 43, ct);
				var expected = Copy(); var original = Item(ResultId); var material = Item(materialId);
				await driver.SendAsync(driver.Api.RemodelItem(npc, original.ObjectId, material.ObjectId), ct);
				await driver.WaitAsync(typeof(SM_INVENTORY_UPDATE_ITEM), p => p.Get<int>("objectId") == original.ObjectId, ct);
				await driver.SynchronizeAsync(ct);
				Consume(expected, material, 1);
				Pay(expected, (driver.Api.World.VendorPrices ?? throw new InvalidDataException("Missing client service prices.")).ServicePrice(1000));
				expected[original.ObjectId] = original with { Details = original.Details with { Enchantment = Enchantment(original) with { SkinId = newSkinId } } };
				AssertInventory(expected, "remodel");
				await driver.SendAsync(driver.Api.CloseDialog(npc), ct);
			}, token);
		}
		async Task UseAsync(int animatedId, int itemId, int scrollId, byte startId, byte finishId, CancellationToken ct)
		{
			await driver.SendAsync(driver.Api.TuneItem(itemId, scrollId), ct);
			bool Own(DecodedBotServerPacket p) => p.Get<int>("playerObjId") == driver.Api.World.SelfObjectId && p.Get<int>("itemObjId") == animatedId;
			var start = await driver.WaitAsync(typeof(SM_ITEM_USAGE_ANIMATION), p => Own(p) && p.Get<byte>("animationId") == startId, ct);
			Require(start.Get<int>("castTime") == 5000, "Identification must take five seconds.");
			await driver.DelayAsync(TimeSpan.FromMilliseconds(start.Get<int>("castTime")), ct);
			await driver.WaitAsync(typeof(SM_ITEM_USAGE_ANIMATION), p => Own(p) && p.Get<byte>("animationId") == finishId, ct);
		}
		async Task OpenServiceAsync(int npc, ushort dialog, CancellationToken ct)
		{
			await driver.SendAsync(driver.Api.Target(npc), ct);
			await driver.SendAsync(driver.Api.TalkTo(npc), ct);
			await driver.WaitAsync(typeof(SM_DIALOG_WINDOW), p => p.Get<int>("targetObjectId") == npc, ct);
			await driver.SendAsync(driver.Api.SelectDialog(npc, dialog), ct);
			await driver.WaitAsync(typeof(SM_DIALOG_WINDOW), p => p.Get<int>("targetObjectId") == npc, ct);
		}
		BotInventoryItem Item(int id) => driver.Api.World.Inventory.Values.Single(item => item.ItemId == id);
		Dictionary<int, BotInventoryItem> Copy() => driver.Api.World.Inventory.ToDictionary();
		void AssertInventory(Dictionary<int, BotInventoryItem> expected, string action) => Require(expected.OrderBy(p => p.Key).SequenceEqual(driver.Api.World.Inventory.OrderBy(p => p.Key)), $"G5 {action} changed unexpected inventory fields.");
	}
	private static BotItemEnchantment Enchantment(BotInventoryItem item) => item.Details.Enchantment ?? throw new InvalidDataException("Missing gear fields.");
	private static void Consume(Dictionary<int, BotInventoryItem> items, BotInventoryItem item, long count)
	{
		Require(item.Count >= count, "Insufficient expected material count.");
		if (item.Count == count) items.Remove(item.ObjectId); else items[item.ObjectId] = item with { Count = item.Count - count };
	}
	private static void Pay(Dictionary<int, BotInventoryItem> items, long amount) => Consume(items, items.Values.Single(item => item.ItemId == BotWorldModel.KinahItemId), amount);
	private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
