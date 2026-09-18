using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private static readonly int[] Q1QuestIds = [1000, 1101, 1102, 1103, 1104, 1100, 1105, 1106];

	private async Task RunQ1Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(
			fixture, policy, "b01", accountId: 36, "Aesimqa", Race.ELYOS);

		session.BeginStep("s01", "login-create-enter-and-finish-prologue");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token);
		await session.EnterWorldAsync(token);
		await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
		await WaitForQuestStatusAsync(session, 1000, 5, token);
		Player player = fixture.World.GetPlayer(session.CharacterId);

		Npc elpas = FindLivingNpc(player, 203049);
		await MoveBesideAsync(session, elpas, token);
		await StartQuestAsync(session, elpas.GetObjectId(), 1101, token);
		Npc mires = FindLivingNpc(player, 203057);
		await MoveBesideAsync(session, mires, token);
		await FinishStandardQuestAsync(session, mires.GetObjectId(), 1101, token);

		await StartQuestAsync(session, mires.GetObjectId(), 1102, token);
		for (int kill = 1; kill <= 3; kill++)
		{
			Npc kerub = FindLivingNpc(player, 210133);
			await MoveBesideAsync(session, kerub, token);
			await KillForQuestAsync(session, player, kerub, token);
		}
		await MoveBesideAsync(session, mires, token);
		await FinishStandardQuestAsync(session, mires.GetObjectId(), 1102, token);

		await StartQuestAsync(session, mires.GetObjectId(), 1103, token);
		for (int sackNumber = 1; sackNumber <= 3; sackNumber++)
		{
			Npc sack = FindLivingNpc(player, 700105);
			await MoveBesideAsync(session, sack, token);
			await LootActionObjectAsync(session, sack.GetObjectId(), 182200201, token);
		}
		Assert.Equal(3, ItemCount(session.Api.World, 182200201));
		await MoveBesideAsync(session, mires, token);
		await FinishItemQuestAsync(session, mires.GetObjectId(), 1103, token);
		Assert.Equal(0, ItemCount(session.Api.World, 182200201));
		Assert.Equal(2, player.GetLevel());
		Assert.True(!session.Api.World.Quests.TryGetValue(1100, out BotQuestState? locked1100) || locked1100.Status != 3);

		await StartQuestAsync(session, mires.GetObjectId(), 1104, token);
		Npc polinia = FindLivingNpc(player, 203059);
		await MoveBesideAsync(session, polinia, token);
		await FinishStandardQuestAsync(session, polinia.GetObjectId(), 1104, token);
		Assert.Equal(3, player.GetLevel());
		await WaitForQuestStatusAsync(session, 1100, 3, token);

		Npc kalio = FindLivingNpc(player, 203067);
		await MoveBesideAsync(session, kalio, token);
		await FinishStandardQuestAsync(session, kalio.GetObjectId(), 1100, token, DialogAction.SELECTED_QUEST_REWARD1);

		Npc kales = FindLivingNpc(player, 203050);
		await MoveBesideAsync(session, kales, token);
		await StartQuestAsync(session, kales.GetObjectId(), 1105, token);
		for (int kill = 1; kill <= 3; kill++)
		{
			Npc snuffler = FindLivingNpc(player, 210079);
			await MoveBesideAsync(session, snuffler, token);
			await KillForQuestAsync(session, player, snuffler, token);
			await LootCorpseItemAsync(session, snuffler.GetObjectId(), 182200202, token);
		}
		Assert.Equal(3, ItemCount(session.Api.World, 182200202));
		await MoveBesideAsync(session, kales, token);
		await FinishItemQuestAsync(session, kales.GetObjectId(), 1105, token);
		Assert.Equal(0, ItemCount(session.Api.World, 182200202));

		await MoveBesideAsync(session, kales, token);
		await StartQuestAsync(session, kales.GetObjectId(), 1106, token);
		Assert.Equal(1, ItemCount(session.Api.World, 182200203));
		Npc uno = FindLivingNpc(player, 203061);
		await MoveBesideAsync(session, uno, token);
		await FinishStandardQuestAsync(session, uno.GetObjectId(), 1106, token);
		Assert.Equal(0, ItemCount(session.Api.World, 182200203));

		foreach (int questId in Q1QuestIds)
			Assert.Equal((byte)5, session.Api.World.Quests[questId].Status);
		AssertQuestStatusTraces(session.PacketHistory);
		policy.AssertClean();
	}

	private static async Task StartQuestAsync(
		SimulationL0Session session, int npcObjectId, int questId, CancellationToken token)
	{
		await session.StartQuestAsync(npcObjectId, questId, token);
		Assert.Equal((byte)3, session.Api.World.Quests[questId].Status);
	}

	private static async Task FinishStandardQuestAsync(
		SimulationL0Session session,
		int npcObjectId,
		int questId,
		CancellationToken token,
		int rewardAction = DialogAction.SELECTED_QUEST_NOREWARD)
	{
		await session.SendPacketAsync(session.Api.TalkTo(npcObjectId), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		await session.SendPacketAsync(session.Api.SelectDialog(npcObjectId, DialogAction.QUEST_SELECT, questId: questId), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		if (session.Api.World.Quests[questId].Status == 3)
		{
			await session.SendPacketAsync(session.Api.SelectDialog(npcObjectId, DialogAction.SELECT_QUEST_REWARD, questId: questId), token);
			await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		}
		await session.SendPacketAsync(session.Api.SelectDialog(npcObjectId, checked((ushort)rewardAction), questId: questId), token);
		await WaitForQuestStatusAsync(session, questId, 5, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
			packet => packet.Get<int>("targetObjectId") == npcObjectId);
	}

	private static async Task FinishItemQuestAsync(
		SimulationL0Session session, int npcObjectId, int questId, CancellationToken token)
	{
		await session.SendPacketAsync(session.Api.TalkTo(npcObjectId), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		await session.SendPacketAsync(session.Api.SelectDialog(npcObjectId, DialogAction.QUEST_SELECT, questId: questId), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		await session.SendPacketAsync(session.Api.SelectDialog(
			npcObjectId, DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: questId), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		Assert.Equal((byte)4, session.Api.World.Quests[questId].Status);
		await session.SendPacketAsync(session.Api.SelectDialog(
			npcObjectId, DialogAction.SELECTED_QUEST_NOREWARD, questId: questId), token);
		await WaitForQuestStatusAsync(session, questId, 5, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
			packet => packet.Get<int>("targetObjectId") == npcObjectId);
	}

	private static async Task KillForQuestAsync(
		SimulationL0Session session, Player player, Npc npc, CancellationToken token)
	{
		npc.GetLifeStats().SetCurrentHp(1);
		await session.SendPacketAsync(session.Api.Target(npc.GetObjectId()), token);
		await session.SendPacketAsync(
			session.Api.Attack(npc.GetObjectId(), player.GetGameStats().GetAttackSpeed().GetCurrent()), token);
		await session.WaitForPacketAsync(typeof(SM_LOOT_STATUS), token,
			packet => packet.Get<int>("targetObjectId") == npc.GetObjectId() &&
				packet.Get<byte>("status") == (byte)SM_LOOT_STATUS.Status.LOOT_ENABLE);
		Assert.True(npc.IsDead());
	}

	private static async Task LootActionObjectAsync(
		SimulationL0Session session, int objectId, int itemId, CancellationToken token)
	{
		await session.SendPacketAsync(session.Api.TalkTo(objectId), token);
		await session.WaitForPacketAsync(typeof(SM_EMOTION), token,
			packet => packet.Get<int>("senderObjectId") == session.CharacterId &&
				packet.Get<byte>("emotionType") == (byte)EmotionType.START_QUESTLOOT);
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(3001), token);
		await LootListedItemAndCloseAsync(session, objectId, itemId, token);
	}

	private static Task LootCorpseItemAsync(
		SimulationL0Session session, int objectId, int itemId, CancellationToken token) =>
		LootOpenItemAndCloseAsync(session, objectId, itemId, token);

	private static async Task LootOpenItemAndCloseAsync(
		SimulationL0Session session, int objectId, int itemId, CancellationToken token)
	{
		await session.SendPacketAsync(session.Api.Loot(objectId), token);
		await LootListedItemAndCloseAsync(session, objectId, itemId, token);
	}

	private static async Task LootListedItemAndCloseAsync(
		SimulationL0Session session, int objectId, int itemId, CancellationToken token)
	{
		DecodedBotServerPacket list = await session.WaitForPacketAsync(typeof(SM_LOOT_ITEMLIST), token,
			packet => packet.Get<int>("targetObjectId") == objectId);
		IReadOnlyDictionary<string, object?> item = list
			.Get<List<IReadOnlyDictionary<string, object?>>>("items")
			.Single(entry => Get<int>(entry, "itemId") == itemId);
		BotInventoryItem? existing = session.Api.World.Inventory.Values.SingleOrDefault(entry => entry.ItemId == itemId);
		await session.SendPacketAsync(session.Api.Loot(objectId, Get<byte>(item, "index")), token);
		if (existing == null)
		{
			await session.WaitForPacketAsync(typeof(SM_INVENTORY_ADD_ITEM), token,
				packet => packet.Get<List<IReadOnlyDictionary<string, object?>>>("items")
					.Any(entry => Get<int>(entry, "itemId") == itemId));
		}
		else
		{
			await session.WaitForPacketAsync(typeof(SM_INVENTORY_UPDATE_ITEM), token,
				packet => packet.Get<int>("objectId") == existing.ObjectId);
		}
		await session.SendPacketAsync(session.Api.Loot(objectId, close: true), token);
	}

	private static async Task MoveBesideAsync(
		SimulationL0Session session, Npc npc, CancellationToken token)
	{
		await session.MoveToPositionAsync(new BotPosition(npc.GetX() - 1, npc.GetY(), npc.GetZ(), npc.GetHeading()), token);
		if (!session.Api.World.Objects.ContainsKey(npc.GetObjectId()))
		{
			await session.WaitForPacketAsync(typeof(SM_NPC_INFO), token,
				packet => packet.Get<int>("objectId") == npc.GetObjectId());
		}
	}

	private static async Task WaitForQuestStatusAsync(
		SimulationL0Session session, int questId, byte status, CancellationToken token)
	{
		if (session.Api.World.Quests.TryGetValue(questId, out BotQuestState? quest) && quest.Status == status)
			return;
		await session.WaitForPacketAsync(typeof(SM_QUEST_ACTION), token,
			packet => packet.Get<int>("questId") == questId &&
				packet.Fields.TryGetValue("status", out object? value) && value is byte actual && actual == status);
	}

	private static long ItemCount(BotWorldModel world, int itemId) =>
		world.Inventory.Values.Where(item => item.ItemId == itemId).Sum(item => item.Count);

	private static void AssertQuestStatusTraces(IReadOnlyList<DecodedBotServerPacket> packets)
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
		var mismatches = new List<string>();
		foreach ((int questId, byte[] expectedStatuses) in expected)
		{
			byte[] statuses = packets
				.Where(packet => packet.PacketType == typeof(SM_QUEST_ACTION) &&
					packet.Get<int>("questId") == questId && packet.Fields.ContainsKey("status"))
				.Select(packet => packet.Get<byte>("status"))
				.ToArray();
			if (!expectedStatuses.SequenceEqual(statuses))
				mismatches.Add($"Q{questId}: expected [{string.Join(",", expectedStatuses)}], actual [{string.Join(",", statuses)}]");
		}
		Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
	}

	private static T Get<T>(IReadOnlyDictionary<string, object?> fields, string name) =>
		fields.TryGetValue(name, out object? value) && value is T typed
			? typed
			: throw new InvalidDataException($"Decoded field '{name}' was missing or was not {typeof(T).Name}.");
}
