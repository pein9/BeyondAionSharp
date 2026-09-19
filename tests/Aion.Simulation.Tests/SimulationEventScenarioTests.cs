using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model;
using Aion.GameServer.Dao;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Event;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunL8EventsAsync(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3)); var token = timeout.Token;
		Assert.Equal(new DateTimeOffset(2026, 8, 9, 23, 50, 0, TimeSpan.Zero), fixture.Epoch);
		var events = EventService.GetInstance();
		Assert.True(events.IsEventActive("Summer Block Party")); Assert.True(events.IsEventActive("Increased XP Rates"));
		Assert.True(events.IsActiveEventQuest(80352));
		var rule = Assert.Single(events.GetActiveEventDropRules(), r => r.GetRuleName() == "Summer Block Party Drop (Shared, Hero)");
		Assert.Equal(100f, rule.GetChance()); Assert.Equal(188100124, Assert.Single(rule.GetDropItems()!).GetId());
		await using var session = new SimulationL0Session(fixture, policy, "b01", 120, "Simevents");
		Step("s01", "login-during-shipped-summer-event-and-receive-event-buff");
		await session.LoginAndAuthenticateAsync(token); await session.CreateCharacterAsync(token);
		await session.EnterWorldAsync(token); await session.SynchronizeAsync(token);
		var player = fixture.World.GetPlayer(session.CharacterId); Assert.Equal(0, player.AccessLevel);
		Assert.True(player.GetEffectController().HasAbnormalEffect(10821)); Assert.True(HasXpBuff(session.PacketHistory));
		Assert.Contains(session.PacketHistory, p => p.PacketType == typeof(SM_MESSAGE) && p.Get<string>("message").Contains("Benefit from increased XP until Sunday!", StringComparison.Ordinal));

		// This is a scoped event/quest/drop test, not the deferred natural-play journey. As in Q1/Q2,
		// level/position and one-hit combat targets are setup; kills, loot and quest rewards use client packets.
		Assert.True(ClassChangeService.SetClass(player, PlayerClass.GLADIATOR, validate: true, updateDaevaStatus: true));
		player.GetCommonData().SetLevel(65); Assert.Equal(65, player.GetLevel());
		Step("s02", "approach-existing-event-npc-and-accept-ice-hot");
		await TeleportForSetupAsync(session, player, 110010000, 1516.9f, 1515.2f, 566.2f, token);
		var giver = Assert.Single(player.GetPosition().GetWorldMapInstance().GetNpcs(831797), n => n.IsSpawned());
		await session.MoveToPositionAsync(new BotPosition(giver.GetX() - 1, giver.GetY(), giver.GetZ(), 0), token);
		var opening = await ExchangeAsync(session.Api.TalkTo(giver.GetObjectId()));
		Assert.True(opening.Any(p => p.PacketType == typeof(SM_DIALOG_WINDOW) && p.Get<int>("targetObjectId") == giver.GetObjectId()),
			"Event NPC did not open its dialog: " + Describe(opening));
		var offer = await ExchangeAsync(session.Api.SelectDialog(giver.GetObjectId(), 31, questId: 80352));
		Assert.True(offer.Any(p => p.PacketType == typeof(SM_DIALOG_WINDOW) && p.Get<int>("questId") == 80352),
			"Event NPC did not offer Ice Hot: " + Describe(offer));
		var accepted = await ExchangeAsync(session.Api.SelectDialog(giver.GetObjectId(), 1002, questId: 80352));
		Assert.True(session.Api.World.Quests.TryGetValue(80352, out var quest) && quest.Status == 3,
			"Event quest did not start: " + Describe(accepted));
		Assert.Equal(0, ItemCount(session.Api.World, 188100124)); Assert.Equal(0, ItemCount(session.Api.World, 188052626));

		var zones = new[] { "BLACK_FIN_CANYON_210070000", "FROSTSHARD_CAVERN_210070000", "WILTFOG_BRIAR_210070000" }.Select(ZoneName.Get).ToArray();
		var instance = fixture.World.GetWorldMap(210070000).GetMainWorldMapInstance();
		var targets = new[] { 235967, 235971 }.Select(id => Assert.Single(instance.GetNpcs(id), n => n.IsSpawned() && !n.IsDead())).ToArray();
		Assert.Equal(2, targets.Length);
		Assert.All(targets, n => { Assert.True(n.IsEnemy(player)); Assert.Equal(NpcRating.HERO, n.GetRating()); Assert.Contains(zones, n.IsInsideZone); });
		int kill = 0;
		foreach (var target in targets)
		{
			Step($"s03-{++kill}", $"kill-existing-event-target-{target.GetNpcId()}-and-loot-ice-blocks");
			await TeleportForSetupAsync(session, player, 210070000, target.GetX() - 5, target.GetY(), target.GetZ(), token);
			await session.MoveToPositionAsync(new BotPosition(target.GetX() - 1, target.GetY(), target.GetZ(), 0), token);
			int previous = checked((int)ItemCount(session.Api.World, 188100124));
			await KillForQuestAsync(session, player, target, token);
			await LootCorpseItemAsync(session, target.GetObjectId(), 188100124, token); await session.SynchronizeAsync(token);
			Assert.Equal(previous + 3, ItemCount(session.Api.World, 188100124));
			Assert.Equal(previous + 3, player.GetInventory().GetItemCountByItemId(188100124));
		}
		Step("s04", "turn-in-six-earned-event-drops-and-receive-existing-reward");
		await TeleportForSetupAsync(session, player, 110010000, giver.GetX() - 5, giver.GetY(), giver.GetZ(), token);
		await session.MoveToPositionAsync(new BotPosition(giver.GetX() - 1, giver.GetY(), giver.GetZ(), 0), token);
		await FinishItemQuestAsync(session, giver.GetObjectId(), 80352, token); await session.SynchronizeAsync(token);
		Assert.Equal(0, ItemCount(session.Api.World, 188100124)); Assert.Equal(1, ItemCount(session.Api.World, 188052626));
		Assert.Equal(1, player.GetInventory().GetItemCountByItemId(188052626));

		Step("s05", "watch-buff-event-expire-at-midnight-with-summer-event-still-active");
		var expiry = new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero);
		Assert.True(SystemClock.UtcNow() < expiry.AddSeconds(-1));
		await session.AdvanceAsync(expiry.AddMilliseconds(-1) - SystemClock.UtcNow(), token); await session.SynchronizeAsync(token);
		Assert.True(events.IsEventActive("Increased XP Rates")); Assert.True(player.GetEffectController().HasAbnormalEffect(10821));
		int start = session.PacketHistory.Count;
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(1), token); await session.SynchronizeAsync(token);
		Assert.False(events.IsEventActive("Increased XP Rates")); Assert.False(player.GetEffectController().HasAbnormalEffect(10821));
		Assert.Contains(session.PacketHistory.Skip(start), p => p.PacketType == typeof(SM_ABNORMAL_STATE));
		Assert.False(HasXpBuff(session.PacketHistory.Skip(start)));
		Assert.True(events.IsEventActive("Summer Block Party")); Assert.True(events.IsActiveEventQuest(80352));
		Assert.Contains(events.GetActiveEventDropRules(), r => r.GetRuleName() == rule.GetRuleName());
		Assert.True(giver.IsSpawned());

		Step("s06", "relogin-persists-event-quest-reward-and-does-not-reapply-expired-buff");
		await session.QuitAsync(token); await session.VerifyOfflineAsync(token); await session.WaitForReentryAsync(token);
		start = session.PacketHistory.Count;
		await session.ReloginAndVerifyPersistenceAsync(token); await session.EnterWorldAsync(token); await session.SynchronizeAsync(token);
		Assert.Equal(0, ItemCount(session.Api.World, 188100124)); Assert.Equal(1, ItemCount(session.Api.World, 188052626));
		Assert.Equal((byte)1, session.Api.World.CompletedQuests[80352].CompleteCount);
		var stored = PlayerQuestListDAO.Load(session.CharacterId).GetQuestState(80352);
		Assert.NotNull(stored); Assert.Equal(QuestStatus.COMPLETE, stored.GetStatus()); Assert.Equal(1, stored.GetCompleteCount());
		Assert.False(HasXpBuff(session.PacketHistory.Skip(start)));
		Assert.False(fixture.World.GetPlayer(session.CharacterId).GetEffectController().HasAbnormalEffect(10821));
		Step("s07", "quit-and-confirm-offline");
		await session.QuitAsync(token); await session.VerifyOfflineAsync(token); policy.AssertClean();
		void Step(string id, string name) { session.BeginStep(id, name); Console.WriteLine($"L8 {id}: {name}"); }
		async Task<DecodedBotServerPacket[]> ExchangeAsync(BotClientPacket packet)
		{
			int from = session.PacketHistory.Count;
			try { await session.SendPacketAsync(packet, token); await session.SynchronizeAsync(token); }
			catch (Exception error) { throw new InvalidOperationException("L8 exchange failed: " + Describe(session.PacketHistory.Skip(from)), error); }
			return session.PacketHistory.Skip(from).ToArray();
		}
		static string Describe(IEnumerable<DecodedBotServerPacket> packets) => string.Join('\n', packets.Select(p => p.PacketType.Name + " " + System.Text.Json.JsonSerializer.Serialize(p.Fields)));

		static bool HasXpBuff(IEnumerable<DecodedBotServerPacket> packets) => packets.Where(p => p.PacketType == typeof(SM_ABNORMAL_STATE))
			.Any(p => p.Get<List<IReadOnlyDictionary<string, object?>>>("effects").Any(e => Get<ushort>(e, "skillId") == 10821));
	}
}
