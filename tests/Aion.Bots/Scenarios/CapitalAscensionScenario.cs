using Aion.Bots.Api;
using Aion.Bots.Movement;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public interface ICapitalAscensionDriver
{
	BotApi Api { get; }
	Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token);
	Task PrepareAsync(CancellationToken token);
	Task SendAsync(BotClientPacket packet, CancellationToken token);
	Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token);
	Task MoveAsync(BotPosition position, CancellationToken token);
	Task CompleteTeleportAsync(int mapId, CancellationToken token);
	Task FlyAsync(BotPosition destination, TimeSpan duration, CancellationToken token);
	Task DelayAsync(TimeSpan duration, CancellationToken token);
	Task SynchronizeAsync(CancellationToken token);
	Task VerifyAsync(CancellationToken token);
}

/// <summary>CAPITAL: level-nine setup at Pernos, then complete 1006 and 1007 entirely through client actions.</summary>
public static class CapitalAscensionScenario
{
	public static readonly BotPosition Pernos = new(241.094f, 1639.46f, 100.375f, 0);
	public static readonly BotPosition Daminu = new(600, 1542, 116.375f, 0);
	public static readonly BotPosition Belpartan = new(85, 191, 231.6508f, 0);

	public static BotMovementPlan CreateQuestFlight(BotPosition start, BotPosition destination, int mapId, TimeSpan duration)
	{
		if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
		float distance = MathF.Sqrt(MathF.Pow(destination.X - start.X, 2) + MathF.Pow(destination.Y - start.Y, 2) + MathF.Pow(destination.Z - start.Z, 2));
		int samples = checked((int)Math.Ceiling(duration.TotalMilliseconds / 500));
		var frames = new List<BotMovementFrame>(samples);
		for (int i = 1; i <= samples; i++)
		{
			float fraction = (float)i / samples;
			var point = new BotPosition(start.X + (destination.X - start.X) * fraction, start.Y + (destination.Y - start.Y) * fraction,
				start.Z + (destination.Z - start.Z) * fraction, destination.Heading);
			frames.Add(new BotMovementFrame(TimeSpan.FromTicks(duration.Ticks * i / samples - duration.Ticks * (i - 1) / samples),
				GameClientPackets.MoveInAir(mapId, point.X, point.Y, point.Z, point.Heading, (int)MathF.Round(distance * fraction)), point));
		}
		return new BotMovementPlan(frames, duration, distance);
	}

	public static async Task RunAsync(ICapitalAscensionDriver driver, CancellationToken token = default)
	{
		await driver.StepAsync("setup-level-nine-at-pernos", driver.PrepareAsync, token);
		await driver.SynchronizeAsync(token);
		Require(driver.Api.World.Level == 9 && State(driver, 1006, 3, 0), "Ascension was not auto-started at level nine.");
		await driver.StepAsync("pernos-bottle-and-cliona-lake", async ct =>
		{
			int pernos = await ApproachAsync(driver, 790001, Pernos, ct);
			await DialogAsync(driver, pernos, 1006, DialogAction.QUEST_SELECT, 1011, ct);
			await SelectAsync(driver, pernos, 1006, DialogAction.SETPRO1, ct);
			await driver.CompleteTeleportAsync(210010000, ct);
			await driver.MoveAsync(new BotPosition(638, 1060, 99.375f, 0), ct);
			await driver.SynchronizeAsync(ct);
			var bottle = driver.Api.World.Inventory.Values.Single(item => item.ItemId == 182200007);
			await driver.SendAsync(GameClientPackets.UseItem(bottle.ObjectId), ct);
			var use = await driver.WaitAsync(typeof(SM_ITEM_USAGE_ANIMATION), packet => packet.Get<int>("itemObjId") == bottle.ObjectId && packet.Get<byte>("animationId") == 0, ct);
			// Quest use advertises 3000 ms, independently of this item's 2000 ms template casting_delay.
			Require(use.Get<int>("castTime") > 0, "Quest bottle did not advertise a use duration.");
			await driver.DelayAsync(TimeSpan.FromMilliseconds(use.Get<int>("castTime") + 1), ct);
			await driver.WaitAsync(typeof(SM_ITEM_USAGE_ANIMATION), packet => packet.Get<int>("itemObjId") == bottle.ObjectId && packet.Get<byte>("animationId") == 1, ct);
			await WaitStateAsync(driver, 1006, 3, 2, ct);
			await driver.SynchronizeAsync(ct);
			Require(ItemCount(driver, 182200007) == 0 && ItemCount(driver, 182200008) == 1, "Bottle use did not produce Cliona water.");
		}, token);
		await driver.StepAsync("daminu-essence-and-return-to-pernos", async ct =>
		{
			int daminu = await ApproachAsync(driver, 730008, Daminu, ct);
			await DialogAsync(driver, daminu, 1006, DialogAction.QUEST_SELECT, 1352, ct);
			await DialogAsync(driver, daminu, 1006, DialogAction.SELECT2_1, 1353, ct);
			await SelectAsync(driver, daminu, 1006, DialogAction.SETPRO2, ct);
			await driver.CompleteTeleportAsync(210010000, ct);
			await driver.SynchronizeAsync(ct);
			Require(State(driver, 1006, 3, 3) && ItemCount(driver, 182200008) == 0 && ItemCount(driver, 182200009) == 1,
				"Daminu did not exchange water for essence.");
		}, token);
		await driver.StepAsync("enter-karamatis-through-quest", async ct =>
		{
			int pernos = await ApproachAsync(driver, 790001, Pernos, ct);
			await DialogAsync(driver, pernos, 1006, DialogAction.QUEST_SELECT, 1693, ct);
			await SelectAsync(driver, pernos, 1006, DialogAction.SETPRO3, ct);
			await driver.CompleteTeleportAsync(310020000, ct);
			await driver.SynchronizeAsync(ct);
			Require(State(driver, 1006, 3, 99) && ItemCount(driver, 182200009) == 0, "Karamatis transition did not consume essence.");
		}, token);
		await driver.StepAsync("belpartan-scripted-flight", async ct =>
		{
			int npc = await ApproachAsync(driver, 205000, Belpartan, ct);
			await SelectAsync(driver, npc, 1006, DialogAction.QUEST_SELECT, ct);
			await driver.WaitAsync(typeof(SM_EMOTION), packet => packet.Get<int>("senderObjectId") == driver.Api.World.SelfObjectId &&
				packet.Get<byte>("emotionType") == (byte)EmotionType.START_FLYTELEPORT && packet.Get<int>("teleportId") == 1001, ct);
			// flypath_template.xml entry 1 is this quest flight: 45 s to the Karamatis arena.
			await driver.FlyAsync(new BotPosition(218.85f, 250.49f, 206.72f, 0), TimeSpan.FromSeconds(45), ct);
			await driver.SendAsync(GameClientPackets.Emotion((byte)EmotionType.LAND_FLYTELEPORT), ct);
			await driver.SynchronizeAsync(ct);
			Require(State(driver, 1006, 3, 51), "The 43-second quest timer did not start the raider stage.");
			Require(driver.Api.World.Objects.Values.Count(npc => npc.TemplateId == 211042) == 4, "Expected four spawned raiders.");
		}, token);
		await driver.StepAsync("defeat-four-raiders-with-normal-attacks", async ct =>
		{
			for (int i = 0; i < 4; i++)
			{
				int target = driver.Api.World.Objects.Values.Where(npc => npc.TemplateId == 211042).OrderBy(npc => npc.ObjectId).First().ObjectId;
				await DefeatAsync(driver, target, ct);
				Require(State(driver, 1006, 3, i == 3 ? 4 : 52 + i), "Raider kill did not advance the quest exactly once.");
			}
		}, token);
		await driver.StepAsync("defeat-orissan-with-normal-attacks", async ct =>
		{
			await driver.SynchronizeAsync(ct);
			int target = driver.Api.World.Objects.Values.Single(npc => npc.TemplateId == 211043).ObjectId;
			await DefeatAsync(driver, target, ct);
			Require(State(driver, 1006, 3, 5), "Orissan did not advance class selection.");
		}, token);
		await driver.StepAsync("choose-gladiator-and-complete-ascension", async ct =>
		{
			int pernos = await ApproachAsync(driver, 790001, new BotPosition(220.6f, 247.8f, 206, 0), ct);
			await DialogAsync(driver, pernos, 1006, DialogAction.QUEST_SELECT, 2034, ct);
			await SelectAsync(driver, pernos, 1006, DialogAction.SETPRO4, ct);
			await driver.WaitAsync(typeof(SM_DIALOG_WINDOW), packet => packet.Get<int>("questId") == 1006, ct);
			await DialogAsync(driver, pernos, 1006, DialogAction.SETPRO5, 5, ct);
			await SelectAsync(driver, pernos, 1006, DialogAction.SELECTED_QUEST_NOREWARD, ct);
			await driver.CompleteTeleportAsync(210010000, ct);
			await driver.SynchronizeAsync(ct);
			Require(State(driver, 1006, 5) && State(driver, 1007, 3, 0), "Ascension completion did not unlock the ceremony.");
			Require(driver.Api.World.Objects[driver.Api.World.SelfObjectId!.Value].PlayerClass == (byte)PlayerClass.GLADIATOR,
				"Quest did not change the character's class.");
		}, token);
		await driver.StepAsync("pernos-sends-player-to-sanctum", async ct =>
		{
			int pernos = await ApproachAsync(driver, 790001, Pernos, ct);
			await DialogAsync(driver, pernos, 1007, DialogAction.QUEST_SELECT, 1011, ct);
			await SelectAsync(driver, pernos, 1007, DialogAction.SETPRO1, ct);
			await driver.CompleteTeleportAsync(110010000, ct);
			await driver.SynchronizeAsync(ct);
			Require(driver.Api.World.MapId == 110010000 && State(driver, 1007, 3, 1), "Quest did not take the player to Sanctum.");
		}, token);
		await driver.StepAsync("leah-jucleas-and-class-trainer-ceremony", async ct =>
		{
			int leah = await ApproachAsync(driver, 203725, new BotPosition(1369.60f, 1512.01f, 569.067f, 0), ct);
			await DialogAsync(driver, leah, 1007, DialogAction.QUEST_SELECT, 1352, ct);
			await MovieAsync(driver, leah, DialogAction.SELECT2_1, 92, ct);
			await SelectAsync(driver, leah, 1007, DialogAction.SETPRO2, ct);
			await WaitStateAsync(driver, 1007, 3, 2, ct);
			int jucleas = await ApproachAsync(driver, 203752, new BotPosition(1390.76f, 1693.14f, 573.286f, 0), ct);
			await DialogAsync(driver, jucleas, 1007, DialogAction.QUEST_SELECT, 1693, ct);
			await MovieAsync(driver, jucleas, DialogAction.SELECT3_1, 91, ct);
			await SelectAsync(driver, jucleas, 1007, DialogAction.SETPRO3, ct);
			await WaitStateAsync(driver, 1007, 4, 10, ct);
			int trainer = await ApproachAsync(driver, 203758, new BotPosition(1427.68f, 1614.14f, 573.706f, 0), ct);
			await DialogAsync(driver, trainer, 1007, DialogAction.QUEST_SELECT, 5, ct);
			var expected = InventoryTotals(driver);
			// quest_data.xml: first Gladiator class reward, five consumables and 250,000 kinah.
			expected[100000652] = expected.GetValueOrDefault(100000652) + 1;
			expected[162001057] = expected.GetValueOrDefault(162001057) + 5;
			expected[BotWorldModel.KinahItemId] = expected.GetValueOrDefault(BotWorldModel.KinahItemId) + 250_000;
			await SelectAsync(driver, trainer, 1007, DialogAction.SELECTED_QUEST_REWARD1, ct);
			await WaitStateAsync(driver, 1007, 5, null, ct);
			await driver.SynchronizeAsync(ct);
			Require(expected.OrderBy(pair => pair.Key).SequenceEqual(InventoryTotals(driver).OrderBy(pair => pair.Key)),
				"Ceremony did not grant exactly the selected class weapon, consumables and kinah.");
			Require(!driver.Api.World.IsDead && driver.Api.World.Level >= 10 && driver.Api.World.MapId == 110010000, "Capital journey did not finish alive as a Daeva.");
			await driver.VerifyAsync(ct);
		}, token);
	}

	private static async Task<int> ApproachAsync(ICapitalAscensionDriver driver, int templateId, BotPosition destination, CancellationToken token)
	{
		await driver.MoveAsync(destination with { X = destination.X - 1 }, token);
		await driver.SynchronizeAsync(token);
		var npc = driver.Api.World.Objects.Values.Where(npc => npc.TemplateId == templateId)
			.OrderBy(npc => MathF.Abs(npc.Position.X - destination.X) + MathF.Abs(npc.Position.Y - destination.Y)).FirstOrDefault();
		Require(npc != null, $"No visible NPC {templateId} at journey waypoint.");
		await driver.SendAsync(driver.Api.TalkTo(npc!.ObjectId), token);
		await driver.WaitAsync(typeof(SM_DIALOG_WINDOW), packet => packet.Get<int>("targetObjectId") == npc.ObjectId, token);
		return npc.ObjectId;
	}
	private static async Task DefeatAsync(ICapitalAscensionDriver driver, int target, CancellationToken token)
	{
		for (int attack = 0; attack < 180 && driver.Api.World.Objects.TryGetValue(target, out var npc); attack++)
		{
			Require(!driver.Api.World.IsDead, "The journey subject died in Karamatis.");
			await driver.MoveAsync(npc.Position with { X = npc.Position.X - 1 }, token);
			await driver.DelayAsync(TimeSpan.FromSeconds(3), token);
			await driver.SendAsync(driver.Api.Target(target), token);
			await driver.SendAsync(driver.Api.Attack(target, 3000), token); // conservative initial-sword cadence
			await driver.SynchronizeAsync(token);
		}
		Require(!driver.Api.World.Objects.ContainsKey(target), "Ascension opponent survived the bounded normal-attack window.");
	}
	private static async Task MovieAsync(ICapitalAscensionDriver driver, int npc, int action, int movie, CancellationToken token)
	{
		await SelectAsync(driver, npc, 1007, action, token);
		await driver.WaitAsync(typeof(SM_PLAY_MOVIE), packet => packet.Get<int>("cutsceneId") == movie, token);
	}
	private static Task SelectAsync(ICapitalAscensionDriver driver, int npc, int quest, int action, CancellationToken token) =>
		driver.SendAsync(driver.Api.SelectDialog(npc, checked((ushort)action), questId: quest), token);
	private static async Task DialogAsync(ICapitalAscensionDriver driver, int npc, int quest, int action, ushort page, CancellationToken token)
	{
		await SelectAsync(driver, npc, quest, action, token);
		await driver.WaitAsync(typeof(SM_DIALOG_WINDOW), packet => packet.Get<int>("questId") == quest && packet.Get<ushort>("dialogPageId") == page, token);
	}
	private static async Task WaitStateAsync(ICapitalAscensionDriver driver, int quest, byte status, int? step, CancellationToken token)
	{
		if (!State(driver, quest, status, step))
			await driver.WaitAsync(typeof(SM_QUEST_ACTION), _ => State(driver, quest, status, step), token);
	}
	private static bool State(ICapitalAscensionDriver driver, int quest, byte status, int? step = null) =>
		driver.Api.World.Quests.TryGetValue(quest, out var state) && state.Status == status && (step == null || (state.StepAndFlags & 0xffffff) == step);
	private static long ItemCount(ICapitalAscensionDriver driver, int id) => driver.Api.World.Inventory.Values.Where(item => item.ItemId == id).Sum(item => item.Count);
	private static Dictionary<int, long> InventoryTotals(ICapitalAscensionDriver driver) => driver.Api.World.Inventory.Values
		.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
	private static void Require(bool condition, string message)
	{
		if (!condition) throw new InvalidDataException(message);
	}
}
