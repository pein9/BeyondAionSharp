using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static readonly int[] Q2QuestIds = [2000, 2101, 2102, 2103, 2104, 2105, 2100];

	private static async Task<int> RunQ2Async(LiveBotOptions options, LiveBotProblemWriter problems,
		CancellationToken cancellationToken)
	{
		await using var actor = new L0Actor(options, problems, 1, Race.ASMODIANS, characterName: "Asliveaq");
		actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "Q2" });
		try
		{
			await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, cancellationToken);
			await actor.StepAsync("create-asmodian-warrior", actor.Session.CreateCharacterAsync, cancellationToken);
			await actor.StepAsync("enter-world-and-finish-prologue", async token =>
			{
				await actor.Session.EnterWorldAsync(token);
				await actor.Session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
				await actor.Session.WaitForQuestStatusAsync(2000, 5, token);
			}, cancellationToken);

			int? channel = PlannedChannel(options, "Q2");
			if (channel != null)
				await actor.StepAsync("isolate-channel", token => actor.Session.ChangeChannelAsync(channel.Value, token), cancellationToken);

			int asak = await MoveToQuestNpcAsync(actor, "move-to-asak", 203500,
				new BotPosition(560.83f, 2788.11f, 299.062f, 0), cancellationToken);
			await actor.StepAsync("accept-quest-2101", token => actor.Session.StartQuestAsync(asak, 2101, token), cancellationToken);
			int vandar = await MoveToQuestNpcAsync(actor, "move-to-vandar", 203504,
				new BotPosition(526.99f, 2775.67f, 295.751f, 0), cancellationToken);
			await actor.StepAsync("complete-quest-2101", token => actor.Session.FinishQuestWithActionAsync(
				vandar, 2101, DialogAction.SELECTED_QUEST_NOREWARD, token), cancellationToken);

			await actor.StepAsync("accept-quest-2102", token => actor.Session.StartQuestAsync(vandar, 2102, token), cancellationToken);
			var defeatedSpriggs = new HashSet<int>();
			BotPosition[] spriggPositions =
			[
				new(458.924f, 2784.15f, 288.893f, 0),
				new(468.672f, 2763.12f, 289.292f, 0),
				new(469.915f, 2770.84f, 288.947f, 0),
				new(424.609f, 2828.68f, 295.793f, 0)
			];
			for (int kill = 0; kill < spriggPositions.Length; kill++)
			{
				int sprigg = await MoveToQuestNpcAsync(actor, $"move-to-sprigg-worker-{kill + 1}", 210363,
					spriggPositions[kill], cancellationToken, defeatedSpriggs);
				await actor.StepAsync($"kill-sprigg-worker-{kill + 1}",
					token => actor.Session.KillNpcAsync(sprigg, token), cancellationToken);
				defeatedSpriggs.Add(sprigg);
			}
			vandar = await MoveToQuestNpcAsync(actor, "return-to-vandar-for-2102", 203504,
				new BotPosition(526.99f, 2775.67f, 295.751f, 0), cancellationToken);
			await actor.StepAsync("complete-quest-2102", token => actor.Session.FinishQuestWithActionAsync(
				vandar, 2102, DialogAction.SELECTED_QUEST_NOREWARD, token), cancellationToken);

			await actor.StepAsync("accept-quest-2103", token => actor.Session.StartQuestAsync(vandar, 2103, token), cancellationToken);
			int guheitun = await MoveToQuestNpcAsync(actor, "move-to-guheitun", 203501,
				new BotPosition(223.975f, 2679.86f, 295.25f, 0), cancellationToken);
			await actor.StepAsync("complete-quest-2103", token => actor.Session.FinishQuestWithActionAsync(
				guheitun, 2103, DialogAction.SELECTED_QUEST_NOREWARD, token), cancellationToken);

			int vanar = await MoveToQuestNpcAsync(actor, "move-to-vanar", 203502,
				new BotPosition(220.15f, 2678.81f, 295.25f, 0), cancellationToken);
			await actor.StepAsync("accept-quest-2104", token => actor.Session.StartQuestAsync(vanar, 2104, token), cancellationToken);
			var usedBaskets = new HashSet<int>();
			BotPosition[] basketPositions =
			[
				new(135.39f, 2643.84f, 306.337f, 0),
				new(140.963f, 2653.23f, 305.977f, 0),
				new(145.208f, 2668.29f, 305.299f, 0)
			];
			for (int basketNumber = 0; basketNumber < basketPositions.Length; basketNumber++)
			{
				int basket = await MoveToQuestNpcAsync(actor, $"move-to-fruit-basket-{basketNumber + 1}", 700124,
					basketPositions[basketNumber], cancellationToken, usedBaskets);
				await actor.StepAsync($"loot-fruit-basket-{basketNumber + 1}",
					token => actor.Session.LootQuestItemAsync(basket, 182203104, openLoot: false, token), cancellationToken);
				usedBaskets.Add(basket);
			}
			RequireItemCount(actor.Session.Api.World, 182203104, 3);
			vanar = await MoveToQuestNpcAsync(actor, "return-to-vanar-for-2104", 203502,
				new BotPosition(220.15f, 2678.81f, 295.25f, 0), cancellationToken);
			await actor.StepAsync("complete-quest-2104", token => actor.Session.FinishItemQuestAsync(vanar, 2104, token), cancellationToken);
			RequireItemCount(actor.Session.Api.World, 182203104, 0);

			await actor.StepAsync("accept-quest-2105", token => actor.Session.StartQuestAsync(vanar, 2105, token), cancellationToken);
			var defeatedSparkies = new HashSet<int>();
			BotPosition[] sparkiePositions =
			[
				new(148.34f, 2692.48f, 305.894f, 0),
				new(168.82f, 2638.2f, 305.185f, 0),
				new(169.49f, 2623.05f, 307.181f, 0)
			];
			for (int kill = 0; kill < sparkiePositions.Length; kill++)
			{
				int sparkie = await MoveToQuestNpcAsync(actor, $"move-to-hill-sparkie-{kill + 1}", 210367,
					sparkiePositions[kill], cancellationToken, defeatedSparkies);
				await actor.StepAsync($"kill-and-loot-hill-sparkie-{kill + 1}", async token =>
				{
					await actor.Session.KillNpcAsync(sparkie, token);
					await actor.Session.LootQuestItemAsync(sparkie, 182203105, openLoot: true, token);
				}, cancellationToken);
				defeatedSparkies.Add(sparkie);
			}
			RequireItemCount(actor.Session.Api.World, 182203105, 3);
			vanar = await MoveToQuestNpcAsync(actor, "return-to-vanar-for-2105", 203502,
				new BotPosition(220.15f, 2678.81f, 295.25f, 0), cancellationToken);
			await actor.StepAsync("complete-quest-2105", token => actor.Session.FinishItemQuestAsync(vanar, 2105, token), cancellationToken);
			RequireItemCount(actor.Session.Api.World, 182203105, 0);

			await actor.Session.WaitForQuestStatusAsync(2100, 3, cancellationToken);
			int ulgorn = await MoveToQuestNpcAsync(actor, "move-to-ulgorn", 203516,
				new BotPosition(589.35f, 2450.09f, 278.375f, 0), cancellationToken);
			await actor.StepAsync("complete-quest-2100", token => actor.Session.FinishQuestWithActionAsync(
				ulgorn, 2100, DialogAction.SELECTED_QUEST_REWARD1, token), cancellationToken);

			foreach (int questId in Q2QuestIds)
			{
				if (!actor.Session.Api.World.Quests.TryGetValue(questId, out BotQuestState? state) || state.Status != 5)
					throw new InvalidDataException($"Q{questId} was not complete at the end of Q2.");
			}
			AssertQ2QuestStatusTraces(actor.Session.PacketHistory);
			await actor.StepAsync("quit", actor.Session.QuitAsync, cancellationToken);

			actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "Q2" });
			Console.WriteLine("LIVE Q2 completed.");
			return 0;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"Q2 failed: {ex}");
			return 1;
		}
	}

	private static void AssertQ2QuestStatusTraces(IReadOnlyList<DecodedBotServerPacket> packets)
	{
		IReadOnlyDictionary<int, byte[]> expected = new Dictionary<int, byte[]>
		{
			[2000] = [3, 5],
			[2101] = [3, 4, 5],
			[2102] = [3, 3, 3, 3, 3, 4, 5],
			[2103] = [3, 4, 5],
			[2104] = [3, 4, 5],
			[2105] = [3, 4, 5],
			[2100] = [6, 3, 4, 5],
		};
		foreach ((int questId, byte[] expectedStatuses) in expected)
		{
			byte[] actual = packets
				.Where(packet => packet.PacketType == typeof(SM_QUEST_ACTION) &&
					packet.Get<int>("questId") == questId && packet.Fields.ContainsKey("status"))
				.Select(packet => packet.Get<byte>("status"))
				.ToArray();
			if (!expectedStatuses.SequenceEqual(actual))
				throw new InvalidDataException(
					$"Q{questId} status trace expected [{string.Join(",", expectedStatuses)}], " +
					$"observed [{string.Join(",", actual)}].");
		}
	}
}
