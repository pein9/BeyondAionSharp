using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public sealed record CookingMaster(Race Race, int MapId, int NpcId, BotPosition Position, int StarterRecipe)
{
	public static CookingMaster Hestia { get; } = new(Race.ELYOS, 110010000, 203784,
		new(1848.07f, 1543.97f, 590.158f, 0), 155001381);
	public static CookingMaster Lainita { get; } = new(Race.ASMODIANS, 120010000, 204100,
		new(1167.16f, 1540.53f, 214.174f, 0), 155006386);
}

public interface ICookingLearnDriver
{
	BotApi Api { get; }
	IReadOnlyList<DecodedBotServerPacket> History { get; }
	Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token);
	Task<int> PrepareAsync(CookingMaster master, CancellationToken token);
	Task MakeLevelTenAsync(CancellationToken token);
	Task SendAsync(BotClientPacket packet, CancellationToken token);
	Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token);
	Task DelayAsync(TimeSpan delay, CancellationToken token);
	Task SynchronizeAsync(CancellationToken token);
	Task VerifyServerStateAsync(CancellationToken token);
}

/// <summary>E4: ordinary-client Cooking refusal, confirmation, fee and automatic recipe, for either race.</summary>
public static class CookingLearnScenario
{
	public const int SkillId = 40001;
	public const int QuestionCode = 900852;
	public const int LearnCost = 3500;

	public static async Task RunAsync(ICookingLearnDriver driver, CookingMaster master, CancellationToken token = default)
	{
		int npc = 0;
		await driver.StepAsync("prepare-level-nine-cooking-student", async stepToken =>
		{
			npc = await driver.PrepareAsync(master, stepToken);
			await driver.SynchronizeAsync(stepToken);
			Require(driver.Api.World.Level == 9, "Cooking refusal requires a level-nine subject.");
			Require(driver.Api.World.Kinah >= LearnCost, "Cooking setup did not supply enough kinah.");
			Require(!driver.Api.World.Skills.ContainsKey(SkillId) && !driver.Api.World.Recipes.Contains(master.StarterRecipe),
				"Subject already knows Cooking or its starter recipe.");
			await driver.SendAsync(driver.Api.TalkTo(npc), stepToken);
			await driver.WaitAsync(typeof(SM_DIALOG_WINDOW), packet => packet.Get<int>("targetObjectId") == npc, stepToken);
		}, token);
		Dictionary<int, long> before = Totals(driver.Api.World);
		var recipesBefore = driver.Api.World.Recipes.ToHashSet();
		await driver.StepAsync("silent-refusal-below-level-ten", async stepToken =>
		{
			int historyStart = driver.History.Count;
			await driver.SendAsync(driver.Api.SelectDialog(npc, 46), stepToken);
			// Watch a window, not just its endpoint. Each time-check drains preceding server notifications.
			for (int i = 0; i < 5; i++)
			{
				await driver.DelayAsync(TimeSpan.FromMilliseconds(200), stepToken);
				await driver.SynchronizeAsync(stepToken);
				Require(!driver.History.Skip(historyStart).Any(packet => packet.PacketType == typeof(SM_QUESTION_WINDOW) ||
					packet.PacketType == typeof(SM_LEARN_RECIPE) || packet.PacketType == typeof(SM_SYSTEM_MESSAGE)),
					"Below-level-ten Cooking request was not silently refused.");
				Require(!driver.Api.World.Skills.ContainsKey(SkillId), "Cooking was learned below level ten.");
				VerifyInventory(driver.Api.World, before);
				Require(recipesBefore.SetEquals(driver.Api.World.Recipes), "Refused learning changed recipes.");
			}
		}, token);
		await driver.StepAsync("prepare-level-ten-and-request-cooking", async stepToken =>
		{
			await driver.MakeLevelTenAsync(stepToken);
			await driver.SynchronizeAsync(stepToken);
			Require(driver.Api.World.Level == 10, "Cooking setup did not reach level ten.");
			// Class setup may auto-learn unrelated morph recipes; isolate the actual Cooking transaction.
			recipesBefore = driver.Api.World.Recipes.ToHashSet();
			before = Totals(driver.Api.World);
			await driver.SendAsync(driver.Api.SelectDialog(npc, 46), stepToken);
			var question = await driver.WaitAsync(typeof(SM_QUESTION_WINDOW), packet => packet.Get<int>("code") == QuestionCode, stepToken);
			Require(question.Get<string[]>("params")[1] == LearnCost.ToString(System.Globalization.CultureInfo.InvariantCulture),
				"Cooking confirmation has the wrong fee.");
			Require(!driver.Api.World.Skills.ContainsKey(SkillId), "Cooking was learned before confirmation.");
			VerifyInventory(driver.Api.World, before);
		}, token);
		await driver.StepAsync("confirm-cooking-and-verify-auto-recipe", async stepToken =>
		{
			await driver.SendAsync(driver.Api.Answer(1), stepToken);
			await driver.WaitAsync(typeof(SM_LEARN_RECIPE), packet => packet.Get<int>("recipeId") == master.StarterRecipe, stepToken);
			await driver.SynchronizeAsync(stepToken);
			Require(driver.Api.World.Skills.TryGetValue(SkillId, out var skill) && skill.Level == 1, "Cooking did not start at skill one.");
			before[BotWorldModel.KinahItemId] -= LearnCost;
			VerifyInventory(driver.Api.World, before);
			recipesBefore.Add(master.StarterRecipe);
			Require(recipesBefore.SetEquals(driver.Api.World.Recipes), "Cooking learned an unexpected set of recipes.");
			await driver.SendAsync(driver.Api.CloseDialog(npc), stepToken);
			await driver.SynchronizeAsync(stepToken);
			Require(driver.Api.Timing.BlockingActivities.Count == 0, "Cooking learning left a blocking interaction.");
			await driver.VerifyServerStateAsync(stepToken);
		}, token);
	}

	private static Dictionary<int, long> Totals(BotWorldModel world) => world.Inventory.Values.GroupBy(item => item.ItemId)
		.ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
	private static void VerifyInventory(BotWorldModel world, Dictionary<int, long> expected) => Require(
		expected.OrderBy(pair => pair.Key).SequenceEqual(Totals(world).OrderBy(pair => pair.Key)), "Cooking changed unexpected item/kinah totals.");
	private static void Require(bool condition, string message)
	{
		if (!condition) throw new InvalidDataException(message);
	}
}
