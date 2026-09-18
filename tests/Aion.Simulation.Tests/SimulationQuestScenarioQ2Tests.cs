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
	private async Task RunQ2Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(
			fixture, policy, "b01", accountId: 37, "Assimqa", Race.ASMODIANS);

		session.BeginStep("s01", "login-create-enter-and-finish-prologue");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token);
		await session.EnterWorldAsync(token);
		await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
		await WaitForQuestStatusAsync(session, 2000, 5, token);
		Player player = fixture.World.GetPlayer(session.CharacterId);

		Npc asak = FindLivingNpc(player, 203500);
		await MoveBesideAsync(session, asak, token);
		await StartQuestAsync(session, asak.GetObjectId(), 2101, token);
		Npc vandar = FindLivingNpc(player, 203504);
		await MoveBesideAsync(session, vandar, token);
		await FinishStandardQuestAsync(session, vandar.GetObjectId(), 2101, token);

		await StartQuestAsync(session, vandar.GetObjectId(), 2102, token);
		for (int kill = 1; kill <= 4; kill++)
		{
			Npc sprigg = FindLivingNpc(player, 210363);
			await MoveBesideAsync(session, sprigg, token);
			await KillForQuestAsync(session, player, sprigg, token);
		}
		await MoveBesideAsync(session, vandar, token);
		await FinishStandardQuestAsync(session, vandar.GetObjectId(), 2102, token);

		await StartQuestAsync(session, vandar.GetObjectId(), 2103, token);
		Npc guheitun = FindLivingNpc(player, 203501);
		await MoveBesideAsync(session, guheitun, token);
		await FinishStandardQuestAsync(session, guheitun.GetObjectId(), 2103, token);

		Npc vanar = FindLivingNpc(player, 203502);
		await MoveBesideAsync(session, vanar, token);
		await StartQuestAsync(session, vanar.GetObjectId(), 2104, token);
		for (int basketNumber = 1; basketNumber <= 3; basketNumber++)
		{
			Npc basket = FindLivingNpc(player, 700124);
			await MoveBesideAsync(session, basket, token);
			await LootActionObjectAsync(session, basket.GetObjectId(), 182203104, token);
		}
		Assert.Equal(3, ItemCount(session.Api.World, 182203104));
		await MoveBesideAsync(session, vanar, token);
		await FinishItemQuestAsync(session, vanar.GetObjectId(), 2104, token);
		Assert.Equal(0, ItemCount(session.Api.World, 182203104));

		await StartQuestAsync(session, vanar.GetObjectId(), 2105, token);
		for (int kill = 1; kill <= 3; kill++)
		{
			Npc sparkie = FindLivingNpc(player, 210367);
			await MoveBesideAsync(session, sparkie, token);
			await KillForQuestAsync(session, player, sparkie, token);
			await LootCorpseItemAsync(session, sparkie.GetObjectId(), 182203105, token);
		}
		Assert.Equal(3, ItemCount(session.Api.World, 182203105));
		await MoveBesideAsync(session, vanar, token);
		await FinishItemQuestAsync(session, vanar.GetObjectId(), 2105, token);
		Assert.Equal(0, ItemCount(session.Api.World, 182203105));

		await WaitForQuestStatusAsync(session, 2100, 3, token);
		Npc ulgorn = FindLivingNpc(player, 203516);
		await MoveBesideAsync(session, ulgorn, token);
		await FinishStandardQuestAsync(session, ulgorn.GetObjectId(), 2100, token, DialogAction.SELECTED_QUEST_REWARD1);

		foreach (int questId in new[] { 2000, 2101, 2102, 2103, 2104, 2105, 2100 })
			Assert.Equal((byte)5, session.Api.World.Quests[questId].Status);
		AssertQ2QuestStatusTraces(session.PacketHistory);
		policy.AssertClean();
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
}
