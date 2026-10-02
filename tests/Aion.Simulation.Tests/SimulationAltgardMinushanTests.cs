using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// AG-04 (docs/natural-altgard-leveling.md): Q2252 "Chasing the Legend" on its contract steps, with a level 20 probe Cleric.
	/// <list type="number">
	/// <item>Sinood's QUEST_ACCEPT_1 gives the Bones of Minushan (182203235);</item>
	/// <item>the bones (700060) take them and raise Minushan's Spirit (210634, 95%) or Minushan Drakie (210635) on their own
	/// spot for 180 s, with the shout 1100630;</item>
	/// <item>a miss: left alone, the monster is gone after its 180 s and Q2252 stays at var 0; Sinood gives new bones (page
	/// 1693);</item>
	/// <item>a second use and a kill (the monster's HP set low on the probe's server: the fight is AG-06's) set REWARD at var 1
	/// for the Spirit or var 2 for the Drakie, and Sinood pays reward group var - 1.</item>
	/// </list>
	/// </summary>
	[SkippableFact]
	public async Task ChasingTheLegendMissesOnceRefillsAndPaysTheKillsRewardGroup()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, level = 20, quest = 2252, drakesMemory = 164000041;
		using var policy = NewPolicy("AG04", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l6");
		NaturalAltgardSpawn minushan = leg.SpawnList.Single(spawn => spawn.QuestId == quest);
		int[] raised = [minushan.NpcId, .. minushan.AlternateNpcIds ?? []];
		NaturalAltgardStep Step(string key) => leg.Steps.Single(step => step.Key == key);
		await using var session = new SimulationL0Session(fixture, policy, "b01", 65, "Asimminushan", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player Server() => fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(Server(), PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		Server().GetCommonData().SetLevel(level);
		SkillLearnService.LearnNewSkills(Server(), 1, level);
		foreach (int id in leg.Start.CompletedQuestIds.Where(id => Server().GetQuestStateList().GetQuestState(id) == null))
			Assert.True(Server().GetQuestStateList().AddQuest(id, new QuestState(id, QuestStatus.COMPLETE)));
		var instance = fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance();
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(instance.GetInstanceId(), Race.ASMODIANS);
		var runtime = new NaturalJourneyRuntime(Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "SIM-ag04", fixture.Seed, fixture.DataManager.StaticData,
			() => fixture.Clock.NowMillis, fixture.Epoch, () => geometry, _ => Task.FromResult(false), policy.AssertClean,
			() => policy.SnapshotProblems(), null!, new Aion.Bots.Dashboard.LiveBotDashboardState());
		(byte Status, int Var)? State(int id) => NaturalAltgardQuestSteps.State(session.Api.World, id);
		long ItemCount(int itemId) => session.Api.World.Inventory.Values.Where(owned => owned.ItemId == itemId).Sum(owned => owned.Count);
		Npc? Raised() => instance.GetNpcs().FirstOrDefault(npc => raised.Contains(npc.GetNpcId()) && !npc.IsDead());
		var log = new List<string>();

		// The fights are AG-06's: each spot is cleared of aggressive monsters first, and what was cleared is logged.
		void ClearNear(BotPosition at, string what)
		{
			var monsters = instance.GetNpcs().Where(npc => !npc.IsDead() && !raised.Contains(npc.GetNpcId()) &&
				NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
				MathF.Pow(npc.GetX() - at.X, 2) + MathF.Pow(npc.GetY() - at.Y, 2) <= 40 * 40).ToArray();
			foreach (var monster in monsters) fixture.World.Despawn(monster);
			if (monsters.Length > 0)
				log.Add($"cleared at {what}: {string.Join(", ", monsters.GroupBy(npc => npc.GetNpcId()).Select(group => $"{group.Key}x{group.Count()}"))}");
		}
		// Java sends no SM_DELETE for what the player saw while it teleports (PlayerController.notSee: "player is teleporting"),
		// so the client drops its view, as it does for a natural teleport. Without that the first, long gone monster stays in
		// view at the bones and is taken for the second.
		async Task SetupTeleportAsync(BotPosition to)
		{
			session.Api.World.BeginWorldReload();
			await TeleportForSetupAsync(session, Server(), altgard, to.X, to.Y, to.Z, token);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
		}
		async Task TeleportNearAsync(float[] at, string what)
		{
			BotPosition ground = geometry.GroundAround(altgard, new BotPosition(at[0], at[1], at[2], 0), [2f, 3f, 5f]).FirstOrDefault() is { } found &&
				found != default ? found : throw new InvalidDataException($"No ground near {what}.");
			ClearNear(ground, what);
			await SetupTeleportAsync(ground);
		}
		async Task TalkAsync(string key)
		{
			NaturalAltgardStep step = Step(key);
			// The shared SIM random sequence can choose the 5% Drakie; Java pays reward group var - 1, whose page is 6.
			if (key == "q2252-reward-sinood" && State(quest) is (4, 2))
				step = step with { Pages = [1352, 6] };
			await TeleportNearAsync(step.Position, "Sinood");
			session.BeginStep($"s-{key}", key);
			log.Add(await NaturalAltgardQuestSteps.TalkAsync(session, step, await session.WaitForNpcAsync(step.NpcId, token), token));
		}
		async Task<Npc> UseBonesAsync(string label)
		{
			await TeleportNearAsync(minushan.Position, "the bones");
			session.BeginStep($"s-bones-{label}", "use-the-bones");
			int bones = await session.WaitForNpcAsync(minushan.TriggerNpcId, token);
			int start = session.PacketHistory.Count;
			Assert.True(await NaturalAltgardQuestSteps.UseObjectAsync(session, bones, null, token), $"the {label} use was interrupted");
			await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
			await session.SynchronizeAsync(token);
			Npc monster = Raised() ?? throw new InvalidOperationException($"the {label} use raised nothing: Q2252 {State(quest)}, bones {ItemCount(minushan.RequiresItemId)}");
			var home = Assert.IsType<Aion.GameServer.Model.Templates.Spawns.SpawnTemplate>(monster.GetSpawn(), exactMatch: false);
			Assert.InRange(MathF.Sqrt(MathF.Pow(home.GetX() - minushan.Position[0], 2) + MathF.Pow(home.GetY() - minushan.Position[1], 2)), 0, 0.5f);
			Assert.Contains(session.PacketHistory.Skip(start), packet => packet.PacketType == typeof(SM_SYSTEM_MESSAGE) && packet.Get<int>("msgId") == 1100630);
			Assert.Equal(0, ItemCount(minushan.RequiresItemId));
			log.Add($"{label} use raised {monster.GetNpcId()} with its shout; the bones are {(instance.GetNpcs().Any(npc => npc.GetObjectId() == bones && !npc.IsDead()) ? "still there" : "gone")}");
			return monster;
		}

		// 1. Sinood: the quest and the bones.
		await TalkAsync("q2252-offer-sinood");
		Assert.Equal(((byte)3, minushan.AtVar), State(quest));
		Assert.Equal(1, ItemCount(minushan.RequiresItemId));

		// 2. The first use, then a miss: away at Sinood, the monster is gone after its 180 s and the quest stays at var 0.
		Npc first = await UseBonesAsync("first");
		long raisedAt = fixture.Clock.NowMillis;
		await TeleportNearAsync(Step("q2252-offer-sinood").Position, "Sinood");
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(raisedAt + (minushan.LifetimeSeconds - 10) * 1000L - fixture.Clock.NowMillis), token);
		await session.SynchronizeAsync(token);
		Assert.True(instance.GetNpcs().Any(npc => npc.GetObjectId() == first.GetObjectId() && !npc.IsDead()), "the monster left before its 180 s");
		await session.AdvanceAsync(TimeSpan.FromSeconds(15), token);
		await session.SynchronizeAsync(token);
		Assert.Null(Raised());
		Assert.Equal(((byte)3, minushan.AtVar), State(quest));
		log.Add($"missed: {first.GetNpcId()} gone after {(fixture.Clock.NowMillis - raisedAt) / 1000} game s left alone; Q2252 {State(quest)}");

		// 3. New bones from Sinood (page 1693).
		session.BeginStep("s-sinood-refill", "q2252-v0-sinood-bones");
		int sinood = await session.WaitForNpcAsync(minushan.RefillNpcId, token);
		await NaturalDialogProtocol.OpenAsync(session, sinood, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == sinood);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(sinood, DialogAction.QUEST_SELECT, questId: quest), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == sinood &&
			packet.Get<ushort>("dialogPageId") == minushan.RefillPage);
		await session.SynchronizeAsync(token);
		await session.SendPacketAsync(session.Api.CloseDialog(sinood), token);
		await session.SynchronizeAsync(token);
		Assert.Equal(1, ItemCount(minushan.RequiresItemId));
		log.Add($"Sinood gave new bones (page {minushan.RefillPage})");

		// 4. The second use and the kill: the monster the client sees, set low on the server and shot from a stand-off.
		Npc second = await UseBonesAsync("second");
		int monsterObject = await session.WaitForNpcAsync(second.GetNpcId(), token);
		Assert.Equal(second.GetObjectId(), monsterObject);
		second.GetLifeStats().SetCurrentHp(1);
		await session.SynchronizeAsync(token);
		bool killed = false;
		for (int attempt = 1; attempt <= 3 && !killed && !second.IsDead(); attempt++)
		{
			BotPosition at = new(second.GetX(), second.GetY(), second.GetZ(), 0);
			BotPosition spot = geometry.GroundAround(altgard, at, [18f, 16f, 20f])
				.First(point => geometry.HasLineOfSight(altgard, point, at with { Z = at.Z + 1 }));
			await SetupTeleportAsync(spot);
			killed = await NaturalAirCombat.ShootDownAsync(session, monsterObject, quest,
				(origin, skill, skillLevel, aim) => runtime.CreateSpellCast(session.Api.World, origin, skill, skillLevel, aim), token, maximumCasts: 10);
			await session.SynchronizeAsync(token);
		}
		Assert.True(killed, $"{second.GetNpcId()} was not killed: dead {second.IsDead()}, HP {second.GetLifeStats().GetCurrentHp()}, Q2252 {State(quest)}");
		int rewardVar = second.GetNpcId() == minushan.NpcId ? 1 : 2;
		Assert.Equal(((byte)4, rewardVar), State(quest));
		log.Add($"killed {second.GetNpcId()}: REWARD at var {rewardVar}");

		// 5. Sinood pays reward group var - 1: Drake's Memory x3 for the Spirit, 1,900 Kinah for the Drakie.
		long kinah = session.Api.World.Kinah, memories = ItemCount(drakesMemory), exp = Server().GetCommonData().GetExp();
		await TalkAsync("q2252-reward-sinood");
		Assert.Contains(quest, session.Api.World.CompletedQuestIds);
		Assert.Equal(rewardVar - 1, Server().GetQuestStateList().GetQuestState(quest).GetRewardGroup());
		Assert.Equal(rewardVar == 1 ? 3 : 0, ItemCount(drakesMemory) - memories);
		Assert.Equal(rewardVar == 1 ? 0 : 1900, session.Api.World.Kinah - kinah);
		log.Add($"reward group {rewardVar - 1}: Drake's Memory +{ItemCount(drakesMemory) - memories}, Kinah +{session.Api.World.Kinah - kinah}, " +
			$"XP +{Server().GetCommonData().GetExp() - exp}");
		Assert.False(Server().IsDead());
		Console.WriteLine($"AG-04 {string.Join("; ", log)}");
		policy.AssertClean();
	}
}
