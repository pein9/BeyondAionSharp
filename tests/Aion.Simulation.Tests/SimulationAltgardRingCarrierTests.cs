using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// AK-04: Q2292 "Making a New Start" by the game clock. Anmurnerk gives it; the Passion, Jealousy and Love Rings drop (100%)
	/// from six named MuMu that exist only in their hours (temporary_spawn). The probe asks NaturalCarrierPolicy what to do
	/// from the client's own game clock (SM_GAME_TIME plus a minute per 5 s): hunt a carrier that is there, or wait for the
	/// soonest window (the Love Ring only by night, AK-Q3), advancing the virtual clock through the wait. It claims the
	/// contract's choice, the Turquoise Earrings. GM setup on the probe only: its class, level and skills, Q24112 complete
	/// (the prerequisite), setup teleports, and each carrier set to 1 HP (the fights are AK-07's).
	/// </summary>
	[SkippableFact]
	public async Task RingCarriersByTheHourCompleteMakingANewStart()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, quest = 2292, anmurnerk = 832822;
		using var policy = NewPolicy("AK04", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l5");
		await using var session = new SimulationL0Session(fixture, policy, "b01", 74, "Asimrings", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player Server() => fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(Server(), PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		Server().GetCommonData().SetLevel(19);
		SkillLearnService.LearnNewSkills(Server(), 1, 19);
		Assert.True(Server().GetQuestStateList().AddQuest(24112, new QuestState(24112, QuestStatus.COMPLETE)));
		var instance = fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance();
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(instance.GetInstanceId(), Race.ASMODIANS);
		var runtime = new NaturalJourneyRuntime(Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "SIM-ak04", fixture.Seed, fixture.DataManager.StaticData,
			() => fixture.Clock.NowMillis, fixture.Epoch, () => geometry, _ => Task.FromResult(false), policy.AssertClean,
			() => policy.SnapshotProblems(), null!, new Aion.Bots.Dashboard.LiveBotDashboardState());
		long ItemCount(int itemId) => session.Api.World.Inventory.Values.Where(owned => owned.ItemId == itemId).Sum(owned => owned.Count);

		async Task TeleportNearAsync(BotPosition at, float[] radii, int skip = 0)
		{
			// Sight from eye height to the target's body, as the server checks a cast (a ground-level check passed a spot the
			// server refused with STR_SKILL_OBSTACLE); a refused spot is skipped on the next try.
			BotPosition ground = geometry.GroundAround(altgard, at, radii)
				.Where(point => geometry.HasLineOfSight(altgard, point with { Z = point.Z + 1.6f }, at with { Z = at.Z + 1 }))
				.Skip(skip).First();
			await TeleportForSetupAsync(session, Server(), altgard, ground.X, ground.Y, ground.Z, token);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
		}
		// The client's game clock: the last SM_GAME_TIME, stamped as it arrived, run on at a minute per 5 s.
		long GameMinutesNow() => session.Api.World.GameMinutesAt(session.Api.Timing.Now)!.Value;

		session.BeginStep("s01", "accept-at-anmurnerk");
		var giver = instance.GetNpcs().First(npc => npc.GetNpcId() == anmurnerk);
		await TeleportNearAsync(new BotPosition(giver.GetX(), giver.GetY(), giver.GetZ(), 0), [2f, 3f, 4f]);
		await session.StartQuestAsync(await session.WaitForNpcAsync(anmurnerk, token), quest, token);
		Assert.Equal(QuestStatus.START, Server().GetQuestStateList().GetQuestState(quest).GetStatus());

		var log = new List<string>();
		int[] rings = leg.TimedSpawnList.Select(carrier => carrier.ItemId).Distinct().Order().ToArray();
		for (int round = 0; round < 8; round++)
		{
			var needed = rings.Where(ring => ItemCount(ring) == 0).ToHashSet();
			long now = GameMinutesNow();
			NaturalCarrierChoice choice = NaturalCarrierPolicy.Decide(new NaturalCarrierObservation(needed, now,
				session.Api.World.Objects.Values.Where(known => known.TemplateId is int id && leg.TimedSpawnList.Any(carrier => carrier.NpcId == id))
					.Select(known => known.TemplateId!.Value).ToHashSet(), 10), leg.TimedSpawnList);
			int server = GameTimeService.GetInstance().GetGameTime().GetTime();
			// The client stamps SM_GAME_TIME when the transport receives it: in SIM, at the end of the advance slice (30 s, 6 game minutes).
			Assert.InRange(server - now, -1, 8);
			log.Add($"{NaturalGameClock.HourOf(now):00}:{now % 60:00} {choice.Action} {choice.Carrier?.NpcId} ring {choice.RingItemId} wait {choice.WaitGameMinutes}");
			if (choice.Action == "done") break;
			if (choice.Action == "wait")
			{
				session.BeginStep($"s-wait-{round}", "wait-for-the-carrier-window");
				for (int seconds = choice.WaitGameMinutes * NaturalGameClock.MillisPerGameMinute / 1000 + 10; seconds > 0; seconds -= 30)
					await session.AdvanceAsync(TimeSpan.FromSeconds(Math.Min(30, seconds)), token);
				await session.SynchronizeAsync(token);
				continue;
			}
			session.BeginStep($"s-hunt-{round}", $"hunt-{choice.Carrier!.NpcId}");
			var carrier = instance.GetNpcs().FirstOrDefault(npc => npc.GetNpcId() == choice.Carrier.NpcId && !npc.IsDead());
			Assert.True(carrier != null, $"The policy chose {choice.Carrier.NpcId}, which the server does not have alive at game hour " +
				$"{GameTimeService.GetInstance().GetGameTime().GetHour()}.");
			bool killed = false;
			int seen = 0, shotAt = 0;
			for (int spot = 0; spot < 4 && !killed && !carrier!.IsDead(); spot++)
			{
				await TeleportNearAsync(new BotPosition(carrier!.GetX(), carrier.GetY(), carrier.GetZ(), 0), [16f, 18f, 14f, 20f], spot);
				seen = await session.WaitForNpcAsync(choice.Carrier.NpcId, token);
				Assert.Equal(carrier.GetObjectId(), seen);
				carrier.GetLifeStats().SetCurrentHp(1);
				shotAt = session.PacketHistory.Count;
				killed = await NaturalAirCombat.ShootDownAsync(session, seen, quest,
					(origin, skill, skillLevel, aim) => runtime.CreateSpellCast(session.Api.World, origin, skill, skillLevel, aim), token, maximumCasts: 6);
			}
			Assert.True(killed && carrier.IsDead(), $"{choice.Carrier.NpcId} was not shot down (killed {killed}, dead {carrier.IsDead()}, HP " +
				$"{carrier.GetLifeStats().GetCurrentHp()}, tribe {carrier.GetObjectTemplate().GetTribe()}, distance " +
				$"{MathF.Sqrt(MathF.Pow(carrier.GetX() - Server().GetX(), 2) + MathF.Pow(carrier.GetY() - Server().GetY(), 2)):F1} m): " +
				string.Join(", ", session.PacketHistory.Skip(shotAt).Where(packet => packet.PacketType != typeof(SM_MOVE)).Select(packet =>
					packet.PacketType == typeof(SM_SYSTEM_MESSAGE) ? $"MSG:{packet.Get<object>("name")}" : packet.PacketType.Name)));
			var corpse = carrier;
			await TeleportNearAsync(new BotPosition(corpse.GetX(), corpse.GetY(), corpse.GetZ(), 0), [2f, 3f, 4f]);
			bool looted = await NaturalAltgardQuestSteps.LootItemAsync(session, seen, choice.Carrier.ItemId, token);
			Assert.True(looted, $"{choice.Carrier.NpcId} dropped no {choice.Carrier.ItemId}.");
			log.Add($"  {choice.Carrier.NpcId} shot down and looted {choice.Carrier.ItemId}");
		}
		Assert.All(rings, ring => Assert.Equal(1, ItemCount(ring)));

		session.BeginStep("s-claim", "claim-at-anmurnerk");
		NaturalAltgardRewardChoice reward = leg.RewardChoiceList.Single(entry => entry.QuestId == quest);
		await TeleportNearAsync(new BotPosition(giver.GetX(), giver.GetY(), giver.GetZ(), 0), [2f, 3f, 4f]);
		int giverObject = await session.WaitForNpcAsync(anmurnerk, token);
		await NaturalDialogProtocol.OpenAsync(session, giverObject, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(giverObject, DialogAction.QUEST_SELECT, questId: quest), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(giverObject, DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: quest), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(giverObject,
			checked((ushort)NaturalAscensionContract.DialogActionId(reward.Action)), questId: quest), token);
		await session.SynchronizeAsync(token);
		await session.SendPacketAsync(session.Api.CloseDialog(giverObject), token);
		await session.SynchronizeAsync(token);
		Assert.Equal(QuestStatus.COMPLETE, Server().GetQuestStateList().GetQuestState(quest).GetStatus());
		Assert.Equal(1, ItemCount(reward.ItemId));
		Assert.All(rings, ring => Assert.Equal(0, ItemCount(ring)));
		// What the runner's own reward chooser would take (AK-08 must honour the contract's choice instead).
		int policyIndex = NaturalIshalgenInventoryPolicy.Load(Aion.GameServer.TestKit.RealStaticData.RepoRoot(), [])
			.ChooseReward(quest, 19, []);
		Console.WriteLine($"AK-04 {string.Join("; ", log)}; claimed {reward.ItemId}; the inventory policy would choose index {policyIndex}");
		policy.AssertClean();
	}
}
