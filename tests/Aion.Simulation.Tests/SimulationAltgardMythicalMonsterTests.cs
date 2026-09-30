using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
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
	/// AB-05 (docs/natural-altgard-leveling.md): Q2223 "A Mythical Monster" on the live SIM server, with a level 16 probe Cleric.
	/// <list type="number">
	/// <item>Lamir's step with Q2231 still open (Lamir gives Q2231 too): does his quest page mask Q2223's?</item>
	/// <item>the Old Incense Burner with the incense: movie 67, and Infernus (EXPERT) at (1547.1, 894.3), gone after 300 s left
	/// alone;</item>
	/// <item>a second incense from Lamir at var 1, the burner back after its respawn, a second Infernus killed (GM: his HP set
	/// low on the probe's server, the real fight is AB-07's), REWARD, and Gefion's reward (the Crystal Earrings).</item>
	/// </list>
	/// </summary>
	[SkippableFact]
	public async Task AltgardMythicalMonsterBurnsTheIncenseAndInfernusComesAndGoes()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, level = 16, quest = 2223, rivalry = 2231, lamir = 203620;
		using var policy = NewPolicy("AB05", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l4");
		NaturalAltgardSpawn infernus = leg.SpawnList.Single(spawn => spawn.QuestId == quest);
		NaturalAltgardStep Step(string key) => leg.Steps.Single(step => step.Key == key);
		await using var session = new SimulationL0Session(fixture, policy, "b01", 147, "Asiminfernus", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player Server() => fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(Server(), PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		Server().GetCommonData().SetLevel(level);
		SkillLearnService.LearnNewSkills(Server(), 1, level);
		var instance = fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance();
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(instance.GetInstanceId(), Race.ASMODIANS);
		var runtime = new NaturalJourneyRuntime(Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "SIM-ab05", fixture.Seed, fixture.DataManager.StaticData,
			() => fixture.Clock.NowMillis, fixture.Epoch, () => geometry, _ => Task.FromResult(false), policy.AssertClean,
			() => policy.SnapshotProblems(), null!, new Aion.Bots.Dashboard.LiveBotDashboardState());
		(byte Status, int Var)? State(int id) => NaturalAltgardQuestSteps.State(session.Api.World, id);
		long ItemCount(int itemId) => session.Api.World.Inventory.Values.Where(owned => owned.ItemId == itemId).Sum(owned => owned.Count);

		async Task NearAsync(int npcId)
		{
			var npc = instance.GetNpcs().First(candidate => candidate.GetNpcId() == npcId && !candidate.IsDead());
			BotPosition ground = geometry.GroundAround(altgard, new BotPosition(npc.GetX(), npc.GetY(), npc.GetZ(), 0), [2f, 3f, 5f]).First();
			await TeleportForSetupAsync(session, Server(), altgard, ground.X, ground.Y, ground.Z, token);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
		}
		async Task<string> TalkAsync(string key)
		{
			NaturalAltgardStep step = Step(key);
			await NearAsync(step.NpcId);
			session.BeginStep($"s-{key}", key);
			return await NaturalAltgardQuestSteps.TalkAsync(session, step, await session.WaitForNpcAsync(step.NpcId, token), token);
		}
		bool InfernusOnServer() => instance.GetNpcs().Any(npc => npc.GetNpcId() == infernus.NpcId && !npc.IsDead());
		async Task BurnAsync(string label)
		{
			await NearAsync(infernus.TriggerNpcId);
			session.BeginStep($"s-burn-{label}", "burn-the-incense");
			int burner = await session.WaitForNpcAsync(infernus.TriggerNpcId, token);
			int start = session.PacketHistory.Count;
			Assert.True(await NaturalAltgardQuestSteps.UseObjectAsync(session, burner, null, token), "The burner use was interrupted.");
			await NaturalMovieGate.FinishAsync(session, token);
			await session.SynchronizeAsync(token);
			bool movie = session.PacketHistory.Skip(start).Any(packet => packet.PacketType == typeof(SM_PLAY_MOVIE) && packet.Get<int>("cutsceneId") == infernus.MovieId);
			await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
			await session.SynchronizeAsync(token);
			var spawned = instance.GetNpcs().FirstOrDefault(npc => npc.GetNpcId() == infernus.NpcId && !npc.IsDead());
			Console.WriteLine($"AB-05 burn {label}: movie {infernus.MovieId} {movie}; incense left {ItemCount(infernus.RequiresItemId)}; " +
				$"Infernus {(spawned is { } s ? $"at ({s.GetX():F1}, {s.GetY():F1}, {s.GetZ():F1})" : "absent")}; burner alive {instance.GetNpcs().Any(npc => npc.GetObjectId() == burner && !npc.IsDead())}");
			Assert.True(movie);
			Assert.NotNull(spawned);
			// He spawns at the handler's point (aggro 15 m, he is already walking to the probe two seconds later).
			var home = spawned!.GetSpawn();
			Assert.InRange(MathF.Sqrt(MathF.Pow(home.GetX() - infernus.Position[0], 2) + MathF.Pow(home.GetY() - infernus.Position[1], 2)), 0, 0.5f);
		}

		// 1. Take Q2231 from Lamir and leave it open, take Q2223 from Gefion, then Lamir's Q2223 step.
		Console.WriteLine($"AB-05 {await TalkAsync("q2231-offer-lamir")}");
		Console.WriteLine($"AB-05 {await TalkAsync("q2223-offer-gefion")}");
		Assert.Equal(((byte)3, 0), State(rivalry));
		Console.WriteLine($"AB-05 with Q2231 open: {await TalkAsync("q2223-v0-lamir")}");
		Assert.Equal(((byte)3, infernus.AtVar), State(quest));
		Assert.Equal(1, ItemCount(infernus.RequiresItemId));

		// 2. Burn it: Infernus appears, and without a fight is gone after his 300 s.
		await BurnAsync("first");
		long spawnedAt = fixture.Clock.NowMillis;
		await session.AdvanceAsync(TimeSpan.FromSeconds(infernus.LifetimeSeconds - 10), token);
		await session.SynchronizeAsync(token);
		Assert.True(InfernusOnServer(), "Infernus left before his 300 s.");
		await session.AdvanceAsync(TimeSpan.FromSeconds(15), token);
		await session.SynchronizeAsync(token);
		Assert.False(InfernusOnServer());
		Console.WriteLine($"AB-05 Infernus gone after {(fixture.Clock.NowMillis - spawnedAt) / 1000} game s untouched; Q2223 still {State(quest)}");
		Assert.Equal(((byte)3, infernus.AtVar), State(quest));

		// 3. A second incense from Lamir (var 1, none in the bag: page 1779).
		await NearAsync(lamir);
		session.BeginStep("s-lamir-refill", "q2223-v1-lamir-incense");
		int lamirObject = await session.WaitForNpcAsync(lamir, token);
		await NaturalDialogProtocol.OpenAsync(session, lamirObject, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == lamirObject);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(lamirObject, DialogAction.QUEST_SELECT, questId: quest), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == lamirObject &&
			packet.Get<ushort>("dialogPageId") == infernus.RefillPage);
		await session.SynchronizeAsync(token);
		await session.SendPacketAsync(session.Api.CloseDialog(lamirObject), token);
		Assert.Equal(1, ItemCount(infernus.RequiresItemId));
		Console.WriteLine($"AB-05 Lamir gave a new incense (page {infernus.RefillPage})");

		// 4. The burner is back (295 s < 300 s), a second burn, and a kill (Infernus's HP set low: the fight is AB-07's).
		await BurnAsync("second");
		var target = instance.GetNpcs().First(npc => npc.GetNpcId() == infernus.NpcId && !npc.IsDead());
		target.GetLifeStats().SetCurrentHp(1);
		await session.SynchronizeAsync(token);
		int infernusObject = await session.WaitForNpcAsync(infernus.NpcId, token);
		bool killed = await NaturalAirCombat.ShootDownAsync(session, infernusObject, quest,
			(origin, skill, skillLevel, aim) => runtime.CreateSpellCast(session.Api.World, origin, skill, skillLevel, aim), token, maximumCasts: 10);
		await session.SynchronizeAsync(token);
		Assert.True(killed, "Infernus was not killed.");
		Assert.Equal((byte)4, State(quest)?.Status);
		Console.WriteLine($"AB-05 Infernus killed: Q2223 {State(quest)}");

		// 5. Gefion's reward.
		Console.WriteLine($"AB-05 {await TalkAsync("q2223-reward-gefion")}");
		Assert.Contains(quest, session.Api.World.CompletedQuestIds);
		NaturalAltgardRewardChoice earrings = leg.RewardChoiceList.Single(choice => choice.QuestId == quest);
		Assert.Contains(session.Api.World.Inventory.Values, owned => owned.ItemId == earrings.ItemId);
		Assert.False(Server().IsDead());
		policy.AssertClean();
	}
}
