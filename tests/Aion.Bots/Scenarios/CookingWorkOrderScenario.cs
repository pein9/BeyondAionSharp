using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public sealed record CookingWorkOrder(CookingMaster Master, int QuestId, int RecipeId, int IssuedItemId,
	int IssuedCount, int ProductId, int DeliverCount, int SkillLevel, int OvenStaticId)
{
	public const int OvenTemplateId = 150000009;
	public const int SaltId = 169400096;
	public bool NeedsSalt => SkillLevel == 10;
	public static IReadOnlyList<CookingWorkOrder> For(CookingMaster master) => master == CookingMaster.Hestia
		? [new(master, 5500, 155004206, 182290205, 4, 182290522, 3, 1, 103),
			new(master, 5501, 155004207, 182290206, 8, 182290523, 6, 10, 103)]
		: [new(master, 6500, 155009206, 182291205, 4, 182291522, 3, 1, 111),
			new(master, 6501, 155009207, 182291206, 8, 182291523, 6, 10, 111)];
}

public interface ICookingWorkOrderDriver : ICookingLearnDriver
{
	Task PrepareWorkOrderAsync(CookingWorkOrder order, CancellationToken token);
	Task MoveBesideAsync(int objectId, CancellationToken token);
	Task AwaitCraftAsync(CancellationToken token);
	Task VerifyWorkOrderAsync(CookingWorkOrder order, CancellationToken token);
}

/// <summary>E5: all four starter Cooking work orders, including the salt variant and quest recipe cleanup.</summary>
public static class CookingWorkOrderScenario
{
	public static async Task RunAsync(ICookingWorkOrderDriver driver, CookingWorkOrder order, CancellationToken token = default)
	{
		int master = 0, oven = 0;
		await driver.StepAsync($"q{order.QuestId}-prepare", async stepToken =>
		{
			await driver.PrepareWorkOrderAsync(order, stepToken);
			await driver.SynchronizeAsync(stepToken);
			master = driver.Api.World.Objects.Values.Single(value => value.TemplateId == order.Master.NpcId).ObjectId;
			// Ovens and doors share SM_GATHERABLE_INFO; select by the advertised template and static id.
			oven = driver.Api.World.Objects.Values.Single(value => value.TemplateId == CookingWorkOrder.OvenTemplateId &&
				value.StaticId == order.OvenStaticId).ObjectId;
			Require(driver.Api.World.Skills[CookingLearnScenario.SkillId].Level >= order.SkillLevel, "Insufficient Cooking setup.");
			Require(!driver.Api.World.Recipes.Contains(order.RecipeId), "Work recipe was present before acceptance.");
			Require(Count(driver, order.IssuedItemId) == 0 && Count(driver, order.ProductId) == 0, "Work-order items were present before acceptance.");
			await driver.MoveBesideAsync(master, stepToken);
		}, token);
		Dictionary<int, long> expected = Totals(driver.Api.World);
		var recipesBefore = driver.Api.World.Recipes.ToHashSet();
		await driver.StepAsync($"q{order.QuestId}-accept-and-receive-components", async stepToken =>
		{
			await driver.SendAsync(driver.Api.TalkTo(master), stepToken);
			await driver.WaitAsync(typeof(SM_DIALOG_WINDOW), packet => packet.Get<int>("targetObjectId") == master, stepToken);
			await driver.SendAsync(driver.Api.SelectDialog(master, 31, questId: order.QuestId), stepToken);
			await driver.WaitAsync(typeof(SM_DIALOG_WINDOW), packet => packet.Get<int>("questId") == order.QuestId, stepToken);
			await driver.SendAsync(driver.Api.SelectDialog(master, 1002, questId: order.QuestId), stepToken);
			await driver.WaitAsync(typeof(SM_LEARN_RECIPE), packet => packet.Get<int>("recipeId") == order.RecipeId, stepToken);
			await driver.SynchronizeAsync(stepToken);
			Require(driver.Api.World.Quests[order.QuestId].Status == 3, "Work order did not enter START.");
			expected[order.IssuedItemId] = order.IssuedCount;
			VerifyInventory(driver, expected);
			Require(recipesBefore.Append(order.RecipeId).ToHashSet().SetEquals(driver.Api.World.Recipes), "Accepting work order changed unexpected recipes.");
			await driver.SendAsync(driver.Api.CloseDialog(master), stepToken);
			await driver.MoveBesideAsync(oven, stepToken);
		}, token);
		for (int index = 1; index <= order.DeliverCount; index++)
		{
			await driver.StepAsync($"q{order.QuestId}-craft-{index}", async stepToken =>
			{
				List<(int ItemId, long Count)> materials = [(order.IssuedItemId, 1)];
				if (order.NeedsSalt) materials.Add((CookingWorkOrder.SaltId, 1));
				await driver.SendAsync(driver.Api.Craft(CookingWorkOrder.OvenTemplateId, order.RecipeId, oven, materials), stepToken);
				var start = await driver.WaitAsync(typeof(SM_CRAFT_UPDATE), packet => packet.Get<int>("itemId") == order.ProductId &&
					(packet.Get<byte>("action") == 0 || packet.Get<byte>("action") >= 4), stepToken);
				Require(start.Get<byte>("action") == 0, "Server refused work-order craft before starting.");
				await driver.AwaitCraftAsync(stepToken);
				var result = await driver.WaitAsync(typeof(SM_CRAFT_UPDATE), packet => packet.Get<int>("itemId") == order.ProductId && packet.Get<byte>("action") >= 4, stepToken);
				Require(result.Get<byte>("action") == 5, "Deterministic work-order crafting did not succeed.");
				await driver.WaitAsync(index == 1 ? typeof(SM_INVENTORY_ADD_ITEM) : typeof(SM_INVENTORY_UPDATE_ITEM),
					_ => Count(driver, order.ProductId) == index, stepToken);
				// Success and item packets precede the timer's final audit/cleanup. A time-check is not a
				// cross-thread barrier; pace consecutive client crafts instead of resubmitting within 1 ms.
				await driver.DelayAsync(TimeSpan.FromMilliseconds(250), stepToken);
				await driver.SynchronizeAsync(stepToken);
				expected[order.IssuedItemId]--;
				expected[order.ProductId] = index;
				if (order.NeedsSalt) expected[CookingWorkOrder.SaltId]--;
				VerifyInventory(driver, expected);
				Require(driver.Api.Timing.BlockingActivities.Count == 0, "Craft completion left a blocking activity.");
			}, token);
		}
		await driver.StepAsync($"q{order.QuestId}-deliver-and-clean-up", async stepToken =>
		{
			Require(Count(driver, order.IssuedItemId) == order.IssuedCount - order.DeliverCount, "Wrong leftover ingredients before delivery.");
			await driver.MoveBesideAsync(master, stepToken);
			await driver.SendAsync(driver.Api.TalkTo(master), stepToken);
			await driver.WaitAsync(typeof(SM_DIALOG_WINDOW), packet => packet.Get<int>("targetObjectId") == master, stepToken);
			await driver.SendAsync(driver.Api.SelectDialog(master, 31, questId: order.QuestId), stepToken);
			await driver.WaitAsync(typeof(SM_DIALOG_WINDOW), packet => packet.Get<int>("questId") == order.QuestId && packet.Get<ushort>("dialogPageId") == 5, stepToken);
			Require(driver.Api.World.Quests[order.QuestId].Status == 4, "Work order did not enter REWARD.");
			await driver.SendAsync(driver.Api.SelectDialog(master, 23, questId: order.QuestId), stepToken);
			await driver.WaitAsync(typeof(SM_QUEST_ACTION), _ => driver.Api.World.CompletedQuestIds.Contains(order.QuestId), stepToken);
			await driver.SynchronizeAsync(stepToken);
			expected.Remove(order.IssuedItemId);
			expected.Remove(order.ProductId);
			VerifyInventory(driver, expected);
			Require(recipesBefore.SetEquals(driver.Api.World.Recipes), "Temporary work recipe survived quest completion.");
			await driver.SendAsync(driver.Api.CloseDialog(master), stepToken);
			await driver.SynchronizeAsync(stepToken);
			await driver.VerifyWorkOrderAsync(order, stepToken);
		}, token);
	}
	private static long Count(ICookingWorkOrderDriver driver, int id) => driver.Api.World.Inventory.Values.Where(item => item.ItemId == id).Sum(item => item.Count);
	private static Dictionary<int, long> Totals(BotWorldModel world) => world.Inventory.Values.GroupBy(item => item.ItemId)
		.ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
	private static void VerifyInventory(ICookingWorkOrderDriver driver, Dictionary<int, long> expected) => Require(
		expected.OrderBy(pair => pair.Key).SequenceEqual(Totals(driver.Api.World).OrderBy(pair => pair.Key)),
		$"Work-order inventory mismatch: expected {string.Join(',', expected.OrderBy(pair => pair.Key))}; actual {string.Join(',', Totals(driver.Api.World).OrderBy(pair => pair.Key))}.");
	private static void Require(bool condition, string message)
	{
		if (!condition) throw new InvalidDataException(message);
	}
}
