using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>AE-00: Q2278's real teleporter, city dialogs and learned Return. Setup teleports only shorten local approaches.</summary>
	[SkippableFact]
	public async Task SecretProposalTravelsToPandaemoniumAndReturnsToSuthran()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		using var policy = NewPolicy("AE00", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.Load(Path.Combine(RealStaticData.RepoRoot(),
			"parity-artifacts", "e2e", "natural-altgard-q2278-probe-contract.json"));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 200, "Asimproposal", Race.ASMODIANS);
		session.BeginStep("s00", "setup-probe");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(player, PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		player.GetCommonData().SetLevel(21);
		SkillLearnService.LearnNewSkills(player, 1, 21);
		player.GetInventory().IncreaseKinah(5000);
		foreach (int id in leg.Start.CompletedQuestIds.Where(id => player.GetQuestStateList().GetQuestState(id) == null))
			Assert.True(player.GetQuestStateList().AddQuest(id, new QuestState(id, QuestStatus.COMPLETE)));
		Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new SM_QUEST_COMPLETED_LIST(0,
			leg.Start.CompletedQuestIds.Select(id => player.GetQuestStateList().GetQuestState(id)).ToList()));
		await session.SynchronizeAsync(token);
		async Task NearAsync(int map, int npcId)
		{
			var instance = fixture.World.GetWorldMap(map).GetMainWorldMapInstance();
			var npc = instance.GetNpcs().First(npc => npc.GetNpcId() == npcId && !npc.IsDead());
			BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(instance.GetInstanceId(), Race.ASMODIANS);
			BotPosition at = geometry.GroundAround(map, new BotPosition(npc.GetX(), npc.GetY(), npc.GetZ(), 0), [2f, 3f, 5f])
				.First(point => point != default);
			foreach (var monster in instance.GetNpcs().Where(monster => !monster.IsDead() &&
				NaturalHostility.IsAggressive(monster.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
				MathF.Pow(monster.GetX() - at.X, 2) + MathF.Pow(monster.GetY() - at.Y, 2) < 1600).ToArray())
				fixture.World.Despawn(monster);
			session.Api.World.BeginWorldReload();
			await TeleportForSetupAsync(session, player, map, at.X, at.Y, at.Z, token);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
		}
		await NearAsync(leg.Hub.MapId, leg.Bind!.NpcId);
		int stone = await session.WaitForNpcAsync(leg.Bind.NpcId, token);
		Assert.True((await new NaturalServiceSteps(session).BindAsync(stone, session.Api.World.Objects[stone].Position,
			leg.Hub.MapId, leg.Bind.Price, leg.Bind.AcceptRange, token)).IsDone);
		var log = new List<string>();
		for (int sequence = 1; sequence <= 12; sequence++)
		{
			NaturalAltgardDecision next = NaturalAltgardDecisionEngine.Decide(leg,
				NaturalAltgardObservation.Observe(session.Api.World, session.CurrentPosition), new Dictionary<int, NaturalTemplateObjective>(), sequence);
			if (session.Api.World.CompletedQuestIds.Contains(2278)) break;
			Assert.Equal("planned", next.Outcome);
			session.BeginStep($"s{sequence:00}", next.Action);
			if (next.Action == "travel-to-map" && next.MapId != leg.Hub.MapId)
			{
				NaturalAltgardMapTrip trip = leg.MapTripList.Single();
				await NearAsync(leg.Hub.MapId, trip.TeleporterNpcId);
				int npc = await session.WaitForNpcAsync(trip.TeleporterNpcId, token);
				NaturalServiceOutcome travelled = await new NaturalServiceSteps(session).TeleportAsync(npc,
					session.Api.World.Objects[npc].Position, trip.TalkRange, trip.LocationId, trip.Fare, trip.MapId, token);
				Assert.True(travelled.IsDone, travelled.Reason);
				log.Add(travelled.Reason);
			}
			else if (next.Action == "travel-to-map")
			{
				BotSkill learned = session.Api.World.Skills[243];
				await session.SendPacketAsync(session.Api.Target(session.CharacterId), token);
				await session.SendPacketAsync(session.Api.Cast(new SpellCastData(243, checked((byte)learned.Level), 0)
					{ TargetObjectId = session.CharacterId }), token);
				DecodedBotServerPacket started = await BotCastProtocol.WaitForStartAsync(
					(predicate, waitToken) => session.WaitForPacketAsync(packet => predicate(packet) || BotCastProtocol.IsStartRejection(packet), waitToken),
					session.CharacterId, 243, token);
				Assert.Equal(typeof(SM_CASTSPELL), started.PacketType);
				session.Api.World.BeginWorldReload();
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(started.Get<ushort>("castDuration") + 1), token);
				DecodedBotServerPacket result = await BotCastProtocol.WaitForCompletionAsync(session.WaitForPacketAsync, session.CharacterId, 243, token);
				Assert.Equal(typeof(SM_CASTSPELL_RESULT), result.PacketType);
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(result.Get<ushort>("hitTime") + 1), token);
				await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
				session.AcceptTeleportPosition();
				await session.SynchronizeAsync(token);
				Assert.Equal(leg.Hub.MapId, session.Api.World.MapId);
				log.Add("Return to fortress bind");
			}
			else
			{
				Assert.Equal("talk", next.Action);
				NaturalAltgardStep step = leg.Steps.Single(step => step.Key == next.StepKey);
				Assert.Equal(leg.StepMap(step), session.Api.World.MapId);
				await NearAsync(leg.StepMap(step), step.NpcId);
				log.Add(await NaturalAltgardQuestSteps.TalkAsync(session, step, await session.WaitForNpcAsync(step.NpcId, token), token));
			}
		}
		Assert.Contains(2278, session.Api.World.CompletedQuestIds);
		Console.WriteLine("AE-00 " + string.Join("; ", log));
		policy.AssertClean();
	}
}
