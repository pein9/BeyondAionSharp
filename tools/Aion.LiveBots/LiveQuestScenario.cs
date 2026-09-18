using System.Text.Json;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static readonly int[] Q1QuestIds = [1000, 1101, 1102, 1103, 1104, 1100, 1105, 1106];

	private static async Task<int> RunQ1Async(LiveBotOptions options, LiveBotProblemWriter problems,
		CancellationToken cancellationToken)
	{
		await using var actor = new L0Actor(options, problems, 1, Race.ELYOS, characterName: "Aeliveaq");
		actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "Q1" });
		try
		{
			await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, cancellationToken);
			await actor.StepAsync("create-elyos-warrior", actor.Session.CreateCharacterAsync, cancellationToken);
			await actor.StepAsync("enter-world-and-finish-prologue", async token =>
			{
				await actor.Session.EnterWorldAsync(token);
				await actor.Session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
				await actor.Session.WaitForQuestStatusAsync(1000, 5, token);
			}, cancellationToken);

			int? channel = PlannedChannel(options, "Q1");
			if (channel != null)
				await actor.StepAsync("isolate-channel", token => actor.Session.ChangeChannelAsync(channel.Value, token), cancellationToken);

			int elpas = await MoveToQuestNpcAsync(actor, "move-to-elpas", 203049,
				new BotPosition(1204.29f, 1053.18f, 138.962f, 0), cancellationToken);
			await actor.StepAsync("accept-quest-1101", token => actor.Session.StartQuestAsync(elpas, 1101, token), cancellationToken);
			int mires = await MoveToQuestNpcAsync(actor, "move-to-mires", 203057,
				new BotPosition(1141f, 1032f, 128.875f, 0), cancellationToken);
			await actor.StepAsync("complete-quest-1101", token => actor.Session.FinishQuestWithActionAsync(
				mires, 1101, DialogAction.SELECTED_QUEST_NOREWARD, token), cancellationToken);

			await actor.StepAsync("accept-quest-1102", token => actor.Session.StartQuestAsync(mires, 1102, token), cancellationToken);
			var defeatedKerubs = new HashSet<int>();
			BotPosition[] kerubPositions =
			[
				new(1098.26f, 1000.43f, 125.6f, 0),
				new(1073.99f, 1057.03f, 125.867f, 0),
				new(1052.9f, 1070.34f, 117.854f, 0)
			];
			for (int kill = 0; kill < kerubPositions.Length; kill++)
			{
				int kerub = await MoveToQuestNpcAsync(actor, $"move-to-striped-kerub-{kill + 1}", 210133,
					kerubPositions[kill], cancellationToken, defeatedKerubs);
				await actor.StepAsync($"kill-striped-kerub-{kill + 1}",
					token => actor.Session.KillNpcAsync(kerub, token), cancellationToken);
				defeatedKerubs.Add(kerub);
			}
			mires = await MoveToQuestNpcAsync(actor, "return-to-mires-for-1102", 203057,
				new BotPosition(1141f, 1032f, 128.875f, 0), cancellationToken);
			await actor.StepAsync("complete-quest-1102", token => actor.Session.FinishQuestWithActionAsync(
				mires, 1102, DialogAction.SELECTED_QUEST_NOREWARD, token), cancellationToken);

			await actor.StepAsync("accept-quest-1103", token => actor.Session.StartQuestAsync(mires, 1103, token), cancellationToken);
			var usedSacks = new HashSet<int>();
			BotPosition[] sackPositions =
			[
				new(1121.36f, 993.115f, 130.656f, 0),
				new(1124.9f, 979.713f, 132.054f, 0),
				new(1130.27f, 978.243f, 132.892f, 0)
			];
			for (int sackNumber = 0; sackNumber < sackPositions.Length; sackNumber++)
			{
				int sack = await MoveToQuestNpcAsync(actor, $"move-to-grain-sack-{sackNumber + 1}", 700105,
					sackPositions[sackNumber], cancellationToken, usedSacks);
				await actor.StepAsync($"loot-grain-sack-{sackNumber + 1}",
					token => actor.Session.LootQuestItemAsync(sack, 182200201, openLoot: false, token), cancellationToken);
				usedSacks.Add(sack);
			}
			RequireItemCount(actor.Session.Api.World, 182200201, 3);
			mires = await MoveToQuestNpcAsync(actor, "return-to-mires-for-1103", 203057,
				new BotPosition(1141f, 1032f, 128.875f, 0), cancellationToken);
			await actor.StepAsync("complete-quest-1103", token => actor.Session.FinishItemQuestAsync(mires, 1103, token), cancellationToken);
			RequireItemCount(actor.Session.Api.World, 182200201, 0);
			if (actor.Session.Api.World.Level != 2 ||
				actor.Session.Api.World.Quests.TryGetValue(1100, out BotQuestState? locked1100) && locked1100.Status == 3)
				throw new InvalidDataException("Q1100 was not locked at level 2 after Q1103.");

			await actor.StepAsync("accept-quest-1104", token => actor.Session.StartQuestAsync(mires, 1104, token), cancellationToken);
			int polinia = await MoveToQuestNpcAsync(actor, "move-to-polinia", 203059,
				new BotPosition(825.436f, 1241.98f, 118.839f, 0), cancellationToken);
			await actor.StepAsync("complete-quest-1104", token => actor.Session.FinishQuestWithActionAsync(
				polinia, 1104, DialogAction.SELECTED_QUEST_NOREWARD, token), cancellationToken);
			if (actor.Session.Api.World.Level != 3)
				throw new InvalidDataException($"Q1104 should reach level 3; observed {actor.Session.Api.World.Level}.");
			await actor.Session.WaitForQuestStatusAsync(1100, 3, cancellationToken);

			int kalio = await MoveToQuestNpcAsync(actor, "move-to-kalio", 203067,
				new BotPosition(820.908f, 1241.05f, 118.682f, 0), cancellationToken);
			await actor.StepAsync("complete-quest-1100", token => actor.Session.FinishQuestWithActionAsync(
				kalio, 1100, DialogAction.SELECTED_QUEST_REWARD1, token), cancellationToken);

			int kales = await MoveToQuestNpcAsync(actor, "move-to-kales", 203050,
				new BotPosition(984.994f, 1133.94f, 108.563f, 0), cancellationToken);
			await actor.StepAsync("accept-quest-1105", token => actor.Session.StartQuestAsync(kales, 1105, token), cancellationToken);
			var defeatedSnufflers = new HashSet<int>();
			BotPosition[] snufflerPositions =
			[
				new(953.314f, 1114.72f, 104.951f, 0),
				new(946.14f, 1066.48f, 107.348f, 0),
				new(925.972f, 1105.12f, 103.18f, 0)
			];
			for (int kill = 0; kill < snufflerPositions.Length; kill++)
			{
				int snuffler = await MoveToQuestNpcAsync(actor, $"move-to-grain-snuffler-{kill + 1}", 210079,
					snufflerPositions[kill], cancellationToken, defeatedSnufflers);
				await actor.StepAsync($"kill-and-loot-grain-snuffler-{kill + 1}", async token =>
				{
					await actor.Session.KillNpcAsync(snuffler, token);
					await actor.Session.LootQuestItemAsync(snuffler, 182200202, openLoot: true, token);
				}, cancellationToken);
				defeatedSnufflers.Add(snuffler);
			}
			RequireItemCount(actor.Session.Api.World, 182200202, 3);
			kales = await MoveToQuestNpcAsync(actor, "return-to-kales-for-1105", 203050,
				new BotPosition(984.994f, 1133.94f, 108.563f, 0), cancellationToken);
			await actor.StepAsync("complete-quest-1105", token => actor.Session.FinishItemQuestAsync(kales, 1105, token), cancellationToken);
			RequireItemCount(actor.Session.Api.World, 182200202, 0);

			await actor.StepAsync("accept-quest-1106", token => actor.Session.StartQuestAsync(kales, 1106, token), cancellationToken);
			await actor.Session.WaitForInventoryItemAsync(182200203, cancellationToken);
			RequireItemCount(actor.Session.Api.World, 182200203, 1);
			int uno = await MoveToQuestNpcAsync(actor, "move-to-uno", 203061,
				new BotPosition(847.263f, 1256.88f, 118.75f, 0), cancellationToken);
			await actor.StepAsync("complete-quest-1106", token => actor.Session.FinishQuestWithActionAsync(
				uno, 1106, DialogAction.SELECTED_QUEST_NOREWARD, token), cancellationToken);
			RequireItemCount(actor.Session.Api.World, 182200203, 0);

			foreach (int questId in Q1QuestIds)
			{
				if (!actor.Session.Api.World.Quests.TryGetValue(questId, out BotQuestState? state) || state.Status != 5)
					throw new InvalidDataException($"Q{questId} was not complete at the end of Q1.");
			}
			AssertQ1QuestStatusTraces(actor.Session.PacketHistory);

			await actor.StepAsync("quit-before-persistence-check", actor.Session.QuitAsync, cancellationToken);
			await actor.StepAsync("verify-player-quest-rows", async token =>
			{
				await actor.Session.VerifyOfflineAsync(token);
				await actor.Session.VerifyQuestRowsAsync(Q1QuestIds, token);
			}, cancellationToken);
			await actor.StepAsync("wait-for-reentry", actor.Session.WaitForReentryAsync, cancellationToken);
			await actor.StepAsync("relogin", actor.Session.ReloginAndVerifyPersistenceAsync, cancellationToken);
			int packetStart = actor.Session.PacketHistory.Count;
			await actor.StepAsync("reenter-and-verify-completed-list", async token =>
			{
				await actor.Session.EnterWorldAsync(token);
				if (!actor.Session.PacketHistory.Skip(packetStart).Any(packet => packet.PacketType == typeof(SM_QUEST_COMPLETED_LIST)))
					throw new InvalidDataException("Relog did not emit SM_QUEST_COMPLETED_LIST.");
				foreach (int questId in Q1QuestIds)
				{
					if (!actor.Session.Api.World.CompletedQuests.ContainsKey(questId))
						throw new InvalidDataException($"SM_QUEST_COMPLETED_LIST omitted Q{questId} after relog.");
				}
			}, cancellationToken);
			QuestCoverageReceipt.SaveFromEnvironment("LIVE", "Q1", actor.Session.Api.World);
			await actor.StepAsync("final-quit", actor.Session.QuitAsync, cancellationToken);

			actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "Q1" });
			Console.WriteLine("LIVE Q1 completed.");
			return 0;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"Q1 failed: {ex}");
			return 1;
		}
	}

	private static async Task<int> MoveToQuestNpcAsync(L0Actor actor, string step, int templateId,
		BotPosition position, CancellationToken cancellationToken, IReadOnlySet<int>? excluded = null)
	{
		await actor.StepAsync(step, token => actor.Session.MoveToPositionAsync(position, token), cancellationToken);
		return await actor.Session.WaitForNearestNpcExceptAsync(
			templateId, excluded ?? new HashSet<int>(), cancellationToken);
	}

	private static void RequireItemCount(BotWorldModel world, int itemId, long expected)
	{
		long actual = world.Inventory.Values.Where(item => item.ItemId == itemId).Sum(item => item.Count);
		if (actual != expected)
			throw new InvalidDataException($"Item {itemId} count was {actual}, expected {expected}.");
	}

	private static void AssertQ1QuestStatusTraces(IReadOnlyList<DecodedBotServerPacket> packets)
	{
		IReadOnlyDictionary<int, byte[]> expected = new Dictionary<int, byte[]>
		{
			[1000] = [3, 5],
			[1101] = [3, 4, 5],
			[1102] = [3, 3, 3, 3, 4, 5],
			[1103] = [3, 4, 5],
			[1104] = [3, 4, 5],
			[1100] = [6, 3, 4, 5],
			[1105] = [3, 4, 5],
			[1106] = [3, 4, 5],
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

internal sealed partial class LiveBotSession
{
	public async Task<int> WaitForNearestNpcExceptAsync(
		int templateId, IReadOnlySet<int> excludedObjectIds, CancellationToken cancellationToken)
	{
		BotPosition position = CurrentPosition;
		BotKnownObject? nearest = api.World.Objects.Values
			.Where(candidate => candidate.Kind == BotKnownObjectKind.Npc && candidate.TemplateId == templateId &&
				!excludedObjectIds.Contains(candidate.ObjectId))
			.OrderBy(candidate => DistanceSquared(position, candidate.Position))
			.FirstOrDefault();
		if (nearest == null)
		{
			int objectId = await WaitForNpcExceptAsync(templateId, excludedObjectIds, cancellationToken);
			nearest = api.World.Objects[objectId];
		}
		float distance = MathF.Sqrt(DistanceSquared(position, nearest.Position));
		if (distance > 30f)
			throw new InvalidDataException(
				$"Nearest NPC template {templateId} was {distance:F1}m away after route completion.");
		return nearest.ObjectId;
	}

	private static float DistanceSquared(BotPosition left, BotPosition right) =>
		MathF.Pow(right.X - left.X, 2) + MathF.Pow(right.Y - left.Y, 2) + MathF.Pow(right.Z - left.Z, 2);

	public async Task MoveToPositionAsync(BotPosition destination, CancellationToken cancellationToken)
	{
		BotPosition start = CurrentPosition;
		float speed = api.World.MovementSpeed
			?? throw new InvalidOperationException("SM_PLAYER_INFO did not provide movement speed.");
		await ExecuteMovementAsync(new Aion.Bots.Movement.BotMover(api.World)
			.CreateGroundPlan(SegmentRoute(start, destination), start, speed), cancellationToken);
		int mapId = api.World.MapId ?? throw new InvalidOperationException("The bot has not observed its map.");
		expectedPosition = new PersistedPosition(mapId, destination.X, destination.Y, destination.Z);
	}

	public async Task WaitForQuestStatusAsync(int questId, byte status, CancellationToken cancellationToken)
	{
		if (api.World.Quests.TryGetValue(questId, out BotQuestState? current) && current.Status == status)
			return;
		await WaitForGamePacketAsync(typeof(SM_QUEST_ACTION), cancellationToken,
			packet => packet.Get<int>("questId") == questId &&
				packet.Fields.TryGetValue("status", out object? value) && value is byte actual && actual == status);
	}

	public async Task FinishQuestWithActionAsync(
		int npcObjectId, int questId, int rewardAction, CancellationToken cancellationToken)
	{
		await SendGameAsync(api.TalkTo(npcObjectId), cancellationToken);
		await WaitForGamePacketAsync(typeof(SM_DIALOG_WINDOW), cancellationToken);
		await SendGameAsync(api.SelectDialog(npcObjectId, DialogAction.QUEST_SELECT, questId: questId), cancellationToken);
		await WaitForGamePacketAsync(typeof(SM_DIALOG_WINDOW), cancellationToken);
		if (api.World.Quests[questId].Status == 3)
		{
			await SendGameAsync(api.SelectDialog(npcObjectId, DialogAction.SELECT_QUEST_REWARD, questId: questId), cancellationToken);
			await WaitForGamePacketAsync(typeof(SM_DIALOG_WINDOW), cancellationToken);
		}
		await SendGameAsync(api.SelectDialog(npcObjectId, checked((ushort)rewardAction), questId: questId), cancellationToken);
		await WaitForQuestStatusAsync(questId, 5, cancellationToken);
		await WaitForGamePacketAsync(typeof(SM_DIALOG_WINDOW), cancellationToken,
			packet => packet.Get<int>("targetObjectId") == npcObjectId);
	}

	public async Task FinishItemQuestAsync(int npcObjectId, int questId, CancellationToken cancellationToken)
	{
		await SendGameAsync(api.TalkTo(npcObjectId), cancellationToken);
		await WaitForGamePacketAsync(typeof(SM_DIALOG_WINDOW), cancellationToken);
		await SendGameAsync(api.SelectDialog(npcObjectId, DialogAction.QUEST_SELECT, questId: questId), cancellationToken);
		await WaitForGamePacketAsync(typeof(SM_DIALOG_WINDOW), cancellationToken);
		await SendGameAsync(api.SelectDialog(npcObjectId, DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: questId), cancellationToken);
		await WaitForGamePacketAsync(typeof(SM_DIALOG_WINDOW), cancellationToken);
		await WaitForQuestStatusAsync(questId, 4, cancellationToken);
		await SendGameAsync(api.SelectDialog(npcObjectId, DialogAction.SELECTED_QUEST_NOREWARD, questId: questId), cancellationToken);
		await WaitForQuestStatusAsync(questId, 5, cancellationToken);
		await WaitForGamePacketAsync(typeof(SM_DIALOG_WINDOW), cancellationToken,
			packet => packet.Get<int>("targetObjectId") == npcObjectId);
	}

	public async Task KillNpcAsync(int npcObjectId, CancellationToken cancellationToken)
	{
		Task<DecodedBotServerPacket> defeated = WaitForGamePacketAsync(typeof(SM_LOOT_STATUS), cancellationToken,
			packet => packet.Get<int>("targetObjectId") == npcObjectId &&
				packet.Get<byte>("status") == (byte)SM_LOOT_STATUS.Status.LOOT_ENABLE);
		await SendGameAsync(api.Target(npcObjectId), cancellationToken);
		for (byte attack = 0; attack < 128 && !defeated.IsCompleted; attack++)
		{
			if (attack > 0 && attack % 2 == 0)
				await MoveToNpcAsync(npcObjectId, cancellationToken);
			TimeSpan delay = api.Timing.TimeUntilAttack(1400);
			if (delay > TimeSpan.Zero)
				await Task.Delay(delay + TimeSpan.FromMilliseconds(5), cancellationToken);
			await SendGameAsync(api.Attack(npcObjectId, 1400, attack), cancellationToken);
			await Task.WhenAny(defeated, Task.Delay(TimeSpan.FromMilliseconds(1450), cancellationToken));
		}
		if (!defeated.IsCompleted)
			throw new InvalidDataException($"NPC {npcObjectId} survived 128 protocol auto-attacks.");
		await defeated;
	}

	public async Task LootQuestItemAsync(
		int objectId, int itemId, bool openLoot, CancellationToken cancellationToken)
	{
		if (openLoot)
			await SendGameAsync(api.Loot(objectId), cancellationToken);
		else
			await SendGameAsync(api.TalkTo(objectId), cancellationToken);
		DecodedBotServerPacket list = await WaitForGamePacketAsync(typeof(SM_LOOT_ITEMLIST), cancellationToken,
			packet => packet.Get<int>("targetObjectId") == objectId);
		IReadOnlyDictionary<string, object?> item = list
			.Get<List<IReadOnlyDictionary<string, object?>>>("items")
			.Single(entry => ReadField<int>(entry, "itemId") == itemId);
		BotInventoryItem? existing = api.World.Inventory.Values.SingleOrDefault(entry => entry.ItemId == itemId);
		await SendGameAsync(api.Loot(objectId, ReadField<byte>(item, "index")), cancellationToken);
		if (existing == null)
		{
			await WaitForGamePacketAsync(typeof(SM_INVENTORY_ADD_ITEM), cancellationToken,
				packet => packet.Get<List<IReadOnlyDictionary<string, object?>>>("items")
					.Any(entry => ReadField<int>(entry, "itemId") == itemId));
		}
		else
		{
			await WaitForGamePacketAsync(typeof(SM_INVENTORY_UPDATE_ITEM), cancellationToken,
				packet => packet.Get<int>("objectId") == existing.ObjectId);
		}
		await SendGameAsync(api.Loot(objectId, close: true), cancellationToken);
	}

	public async Task WaitForInventoryItemAsync(int itemId, CancellationToken cancellationToken)
	{
		if (api.World.Inventory.Values.Any(item => item.ItemId == itemId))
			return;
		await WaitForGamePacketAsync(typeof(SM_INVENTORY_ADD_ITEM), cancellationToken,
			packet => packet.Get<List<IReadOnlyDictionary<string, object?>>>("items")
				.Any(entry => ReadField<int>(entry, "itemId") == itemId));
	}

	public async Task VerifyQuestRowsAsync(IEnumerable<int> questIds, CancellationToken cancellationToken)
	{
		using var client = new HttpClient { BaseAddress = options.AdminBaseUri };
		using var request = new HttpRequestMessage(HttpMethod.Get,
			$"admin/player-state?characterName={Uri.EscapeDataString(characterName)}");
		request.Headers.Add("X-Admin-Token", options.AdminToken);
		using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();
		await using Stream content = await response.Content.ReadAsStreamAsync(cancellationToken);
		using JsonDocument document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
		Dictionary<int, JsonElement> rows = document.RootElement.GetProperty("quests").EnumerateArray()
			.ToDictionary(row => row.GetProperty("questId").GetInt32(), row => row.Clone());
		foreach (int questId in questIds)
		{
			if (!rows.TryGetValue(questId, out JsonElement row))
				throw new InvalidDataException($"player_quests omitted Q{questId}.");
			if (!string.Equals(row.GetProperty("status").GetString(), "COMPLETE", StringComparison.Ordinal) ||
				row.GetProperty("completeCount").GetInt32() != 1)
				throw new InvalidDataException($"player_quests Q{questId} was not COMPLETE with complete_count 1.");
		}
	}

	private static T ReadField<T>(IReadOnlyDictionary<string, object?> fields, string name) =>
		fields.TryGetValue(name, out object? value) && value is T typed
			? typed
			: throw new InvalidDataException($"Decoded field '{name}' was missing or was not {typeof(T).Name}.");
}
