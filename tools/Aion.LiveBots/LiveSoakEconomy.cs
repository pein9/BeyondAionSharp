using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static readonly Lazy<long> SoakBandagePrice = new(() => VendorScenario.ReadBasePrice());
	private static readonly Lazy<long> SoakBandageStack = new(() => VendorScenario.ReadMaxStack());
	private static readonly Lazy<IReadOnlyDictionary<int, long>> SoakIngredientPrices = new(() =>
		SoakCookingCatalog.ShopMaterials.ToDictionary(id => id, id => VendorScenario.ReadBasePrice(id)));

	private static async Task SoakIndependentPairAsync(L0Actor first, L0Actor second,
		Func<L0Actor, CancellationToken, Task> operation, CancellationToken token)
	{
		using var cancel = CancellationTokenSource.CreateLinkedTokenSource(token);
		int finished = 0;
		await Task.WhenAll(RunAsync(first), RunAsync(second));
		async Task RunAsync(L0Actor actor)
		{
			try
			{
				await operation(actor, cancel.Token);
				Interlocked.Increment(ref finished);
				// The quicker crafter still behaves like a connected client while its peer finishes.
				// There is exactly one reader for this session, never a competing background pump.
				while (Volatile.Read(ref finished) < 2)
				{
					await actor.Session.SynchronizeAsync(cancel.Token);
					await Task.Delay(500, cancel.Token);
				}
			}
			catch { await cancel.CancelAsync(); throw; }
		}
	}

	private sealed class SoakVendorDriver(L0Actor actor) : IVendorScenarioDriver
	{
		public BotApi Api => actor.Session.Api;
		public long BasePrice => SoakBandagePrice.Value;
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token) => actor.StepAsync(action, operation, token);
		public async Task<int> PrepareAsync(CancellationToken token)
		{
			int npc = await actor.Session.WaitForNpcAsync(VendorScenario.VendorId, token);
			await actor.Session.MoveToKnownObjectAsync(npc, token);
			return npc;
		}
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => actor.Session.SendPacketAsync(packet, token);
		public Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) => actor.Session.WaitForPacketAsync(type, token, predicate);
		public Task SynchronizeAsync(CancellationToken token) => actor.Session.SynchronizeAsync(token);
		public async Task VerifyServerStateAsync(IReadOnlyDictionary<int, long> expected, CancellationToken token)
		{
			if (Api.Timing.BlockingActivities.Count != 0) throw new InvalidDataException("Soak vendor left a blocking activity.");
			await actor.Session.ConsolidateSoakStacksAsync(VendorScenario.ItemId, SoakBandageStack.Value, token);
		}
	}

	private static async Task SoakCookingAsync(L0Actor actor, CookingMaster master, CancellationToken token)
	{
		var session = actor.Session;
		var world = session.Api.World;
		int npc = await session.WaitForNpcAsync(master.NpcId, token);
		await session.MoveToKnownObjectAsync(npc, token);
		await session.SynchronizeAsync(token);
		if (!world.Skills.ContainsKey(CookingLearnScenario.SkillId))
		{
			var before = SoakTotals(world);
			await session.SendPacketAsync(session.Api.TalkTo(npc), token);
			await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == npc);
			await session.SendPacketAsync(session.Api.SelectDialog(npc, 46), token);
			var question = await session.WaitForPacketAsync(typeof(SM_QUESTION_WINDOW), token, packet => packet.Get<int>("code") == CookingLearnScenario.QuestionCode);
			if (question.Get<string[]>("params")[1] != CookingLearnScenario.LearnCost.ToString(System.Globalization.CultureInfo.InvariantCulture))
				throw new InvalidDataException("Unexpected Cooking fee.");
			await session.SendPacketAsync(session.Api.Answer(1), token);
			await session.WaitForPacketAsync(typeof(SM_LEARN_RECIPE), token, packet => packet.Get<int>("recipeId") == master.StarterRecipe);
			await session.SynchronizeAsync(token);
			before[BotWorldModel.KinahItemId] -= CookingLearnScenario.LearnCost;
			SoakAssertInventory(world, before);
			if (world.Skills[CookingLearnScenario.SkillId].Level != 1) throw new InvalidDataException("Cooking did not start at level one.");
			await session.SendPacketAsync(session.Api.CloseDialog(npc), token);
		}

		var selected = SoakCookingCatalog.Select(master, world.Skills[CookingLearnScenario.SkillId].Level);
		var order = selected.Order;
		actor.Trace.WriteAction(actor.LastStep, "soak:work-order", new Dictionary<string, object?>
		{ ["quest"] = order.QuestId, ["recipe"] = order.RecipeId, ["requiredSkill"] = order.SkillLevel, ["actualSkill"] = world.Skills[CookingLearnScenario.SkillId].Level });
		await BuyIngredientsAsync();
		var expected = SoakTotals(world);
		if (expected.GetValueOrDefault(order.IssuedItemId) != 0 || expected.GetValueOrDefault(order.ProductId) != 0 || world.Recipes.Contains(order.RecipeId))
			throw new InvalidDataException("Previous work order left ingredients, products or its temporary recipe.");
		var recipes = world.Recipes.ToHashSet();
		int oven = world.Objects.Values.Single(value => value.TemplateId == CookingWorkOrder.OvenTemplateId && value.StaticId == order.OvenStaticId).ObjectId;
		await AcceptAsync();
		while (expected.GetValueOrDefault(order.ProductId) < order.DeliverCount)
		{
			if (expected.GetValueOrDefault(order.IssuedItemId) == 0)
			{
				// Java abandonQuest removes issued materials/recipe, not crafted products. Keep those products.
				await session.SendPacketAsync(session.Api.DeleteQuest(order.QuestId), token);
				await session.WaitForPacketAsync(typeof(SM_QUEST_ACTION), token, packet => packet.Get<int>("questId") == order.QuestId && packet.Get<byte>("action") == 3);
				await session.SynchronizeAsync(token);
				if (world.Recipes.Contains(order.RecipeId)) throw new InvalidDataException("Abandoned work recipe survived.");
				SoakAssertInventory(world, expected);
				actor.Trace.WriteAction(actor.LastStep, "soak:work-order-retry", new Dictionary<string, object?> { ["quest"] = order.QuestId });
				await BuyIngredientsAsync();
				expected = SoakTotals(world);
				await AcceptAsync();
			}
			int skill = world.Skills[CookingLearnScenario.SkillId].Level;
			if (selected.Materials.Any(material => expected.GetValueOrDefault(material.Key) < material.Value))
				throw new InvalidDataException("Cooking replenishment did not cover the next craft.");
			await session.SendPacketAsync(session.Api.Craft(CookingWorkOrder.OvenTemplateId, order.RecipeId, oven,
				selected.Materials.Select(item => (item.Key, item.Value)).ToArray()), token);
			var start = await session.WaitForPacketAsync(typeof(SM_CRAFT_UPDATE), token,
				packet => packet.Get<int>("itemId") == order.ProductId && (packet.Get<byte>("action") == 0 || packet.Get<byte>("action") >= 4));
			if (start.Get<byte>("action") != 0) throw new InvalidDataException("Server refused a prepared soak craft.");
			var result = await session.WaitForPacketAsync(typeof(SM_CRAFT_UPDATE), token,
				packet => packet.Get<int>("itemId") == order.ProductId && packet.Get<byte>("action") >= 4);
			byte outcome = result.Get<byte>("action");
			if (outcome is not (5 or 6)) throw new InvalidDataException($"Soak craft aborted unexpectedly ({outcome}).");
			foreach (var material in selected.Materials) SoakAdd(expected, material.Key, -material.Value);
			if (outcome == 5)
			{
				long previous = expected.GetValueOrDefault(order.ProductId);
				SoakAdd(expected, order.ProductId, 1);
				await session.WaitForPacketAsync(previous == 0 ? typeof(SM_INVENTORY_ADD_ITEM) : typeof(SM_INVENTORY_UPDATE_ITEM), token,
					_ => world.Inventory.Values.Where(item => item.ItemId == order.ProductId).Sum(item => item.Count) == previous + 1);
			}
			await Task.Delay(250, token); // Craft task final cleanup follows result packets on its timer thread.
			await session.SynchronizeAsync(token);
			SoakAssertInventory(world, expected);
			if (session.Api.Timing.BlockingActivities.Count != 0) throw new InvalidDataException("Soak craft failed to release its interaction.");
			actor.Trace.WriteAction(actor.LastStep, "soak:craft-outcome", new Dictionary<string, object?>
			{ ["recipe"] = order.RecipeId, ["skillBefore"] = skill, ["skillDifference"] = skill - order.SkillLevel, ["success"] = outcome == 5 });
		}
		await session.MoveToKnownObjectAsync(npc, token);
		await session.SendPacketAsync(session.Api.TalkTo(npc), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == npc);
		await session.SendPacketAsync(session.Api.SelectDialog(npc, 31, questId: order.QuestId), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("questId") == order.QuestId && packet.Get<ushort>("dialogPageId") == 5);
		await session.SendPacketAsync(session.Api.SelectDialog(npc, 23, questId: order.QuestId), token);
		await session.WaitForPacketAsync(typeof(SM_QUEST_ACTION), token, packet => packet.Get<int>("questId") == order.QuestId &&
			packet.Fields.TryGetValue("status", out object? status) && status is byte value && value == 5);
		await session.SynchronizeAsync(token);
		expected.Remove(order.IssuedItemId); expected.Remove(order.ProductId);
		if (!recipes.SetEquals(world.Recipes)) throw new InvalidDataException("Work order changed unrelated recipes or left its temporary recipe.");
		var reward = SoakCookingRewards.ValidateDelta(expected, SoakTotals(world), SoakCookingRewards.For(order));
		await session.SendPacketAsync(session.Api.CloseDialog(npc), token);
		if (reward is { } bonus && !SoakCookingCatalog.ShopMaterials.Contains(bonus.Key))
		{
			// Ordinary inventory housekeeping, not GM replenishment or hidden state reset.
			if (expected.GetValueOrDefault(bonus.Key) != 0) throw new InvalidDataException("Refusing to discard a preexisting item stack.");
			foreach (var item in world.Inventory.Values.Where(item => item.ItemId == bonus.Key).ToArray())
				await session.SendPacketAsync(GameClientPackets.DeleteItem(item.ObjectId), token);
			await session.SynchronizeAsync(token);
			actor.Trace.WriteAction(actor.LastStep, "soak:discard-work-order-bonus", new Dictionary<string, object?> { ["item"] = bonus.Key, ["count"] = bonus.Value });
		}
		else if (reward is { } ingredientBonus) SoakAdd(expected, ingredientBonus.Key, ingredientBonus.Value);
		SoakAssertInventory(world, expected);

		async Task BuyIngredientsAsync()
		{
			var totals = SoakTotals(world);
			var purchases = selected.Materials.Where(material => material.Key != order.IssuedItemId &&
				totals.GetValueOrDefault(material.Key) < material.Value * order.IssuedCount)
				.Select(material => (ItemId: material.Key, Count: 100L - totals.GetValueOrDefault(material.Key))).ToArray();
			if (purchases.Length == 0) return;
			var (vendorId, tab) = master == CookingMaster.Hestia ? (203785, 68) : (204101, 99);
			int vendor = await session.WaitForNpcAsync(vendorId, token);
			await session.MoveToKnownObjectAsync(vendor, token);
			await session.SendPacketAsync(session.Api.TalkTo(vendor), token);
			await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == vendor);
			await session.SendPacketAsync(session.Api.SelectDialog(vendor, 2), token);
			var list = await session.WaitForPacketAsync(typeof(SM_TRADELIST), token, packet => packet.Get<int>("targetObjectId") == vendor);
			if (!list.Get<int[]>("tabs").Contains(tab)) throw new InvalidDataException("Cooking merchant did not offer its ordinary ingredients tab.");
			var prices = world.VendorPrices ?? throw new InvalidDataException("Missing observed vendor prices.");
			long cost = purchases.Sum(item => prices.BuyPrice(SoakIngredientPrices.Value[item.ItemId], list.Get<int>("buyPriceModifier")) * item.Count);
			if (cost <= 0 || totals[BotWorldModel.KinahItemId] < cost) throw new InvalidDataException("Cooking merchant replenishment exceeded available kinah.");
			await session.SendPacketAsync(session.Api.Buy(vendor, purchases), token);
			await session.SynchronizeAsync(token);
			foreach (var item in purchases) SoakAdd(totals, item.ItemId, item.Count);
			SoakAdd(totals, BotWorldModel.KinahItemId, -cost);
			SoakAssertInventory(world, totals);
			await session.SendPacketAsync(session.Api.CloseDialog(vendor), token);
			actor.Trace.WriteAction(actor.LastStep, "soak:buy-cooking-ingredients", new Dictionary<string, object?> { ["vendor"] = vendorId, ["cost"] = cost });
		}

		async Task AcceptAsync()
		{
			await session.MoveToKnownObjectAsync(npc, token);
			await session.SendPacketAsync(session.Api.TalkTo(npc), token);
			await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == npc);
			await session.SendPacketAsync(session.Api.SelectDialog(npc, 31, questId: order.QuestId), token);
			await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("questId") == order.QuestId);
			await session.SendPacketAsync(session.Api.SelectDialog(npc, 1002, questId: order.QuestId), token);
			await session.WaitForPacketAsync(typeof(SM_LEARN_RECIPE), token, packet => packet.Get<int>("recipeId") == order.RecipeId);
			await session.SynchronizeAsync(token);
			SoakAdd(expected, order.IssuedItemId, order.IssuedCount);
			SoakAssertInventory(world, expected);
			if (world.Quests[order.QuestId].Status != 3) throw new InvalidDataException("Work order was not started.");
			await session.SendPacketAsync(session.Api.CloseDialog(npc), token);
			await session.MoveToKnownObjectAsync(oven, token);
		}
	}

	private static Dictionary<int, long> SoakTotals(BotWorldModel world) => world.Inventory.Values.GroupBy(item => item.ItemId)
		.ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
	private static void SoakAdd(Dictionary<int, long> totals, int id, long delta)
	{
		totals[id] = totals.GetValueOrDefault(id) + delta;
		if (totals[id] == 0) totals.Remove(id);
	}
	private static void SoakAssertInventory(BotWorldModel world, Dictionary<int, long> expected)
	{
		if (!expected.OrderBy(pair => pair.Key).SequenceEqual(SoakTotals(world).OrderBy(pair => pair.Key)))
			throw new InvalidDataException($"Soak inventory mismatch. Expected {string.Join(',', expected)}; actual {string.Join(',', SoakTotals(world))}.");
	}
}
