using System.Xml.Linq;
using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>AE-03: both Spirit pool spots; one expired and one completed Q2263 timer. GM setup stays on free probe accounts.</summary>
	[SkippableTheory]
	[InlineData(198, 0)]
	[InlineData(197, 1)]
	public async Task EastGateSpiritDialogsAndPollenTimerPlayThroughTheContract(int account, int poolSpot)
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int map = 220030000, pollen = 182203242;
		using var policy = NewPolicy($"AE03-{poolSpot}", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l7");
		await using var session = new SimulationL0Session(fixture, policy, "b01", account, $"Asimspirit{(char)('a' + poolSpot)}", Race.ASMODIANS);
		session.BeginStep("s00", "setup-script-probe");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		var player = fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(player, PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		player.GetCommonData().SetLevel(21);
		SkillLearnService.LearnNewSkills(player, 1, 21);
		foreach (int quest in leg.Start.CompletedQuestIds.Append(2278).Where(id => player.GetQuestStateList().GetQuestState(id) == null))
			Assert.True(player.GetQuestStateList().AddQuest(quest, new QuestState(quest, QuestStatus.COMPLETE)));
		var instance = fixture.World.GetWorldMap(map).GetMainWorldMapInstance();
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(instance.GetInstanceId(), Race.ASMODIANS);
		var runtime = new NaturalJourneyRuntime(RealStaticData.RepoRoot(), $"SIM-ae03-{poolSpot}", fixture.Seed, fixture.DataManager.StaticData,
			() => fixture.Clock.NowMillis, fixture.Epoch, () => geometry, _ => Task.FromResult(false), policy.AssertClean,
			() => policy.SnapshotProblems(), null!, new Aion.Bots.Dashboard.LiveBotDashboardState());
		long Items() => session.Api.World.Inventory.Values.Where(item => item.ItemId == pollen).Sum(item => item.Count);
		var log = new List<string>();
		async Task NearAsync(Npc npc, float radius = 3)
		{
			BotPosition at = geometry.GroundAround(map, new BotPosition(npc.GetX(), npc.GetY(), npc.GetZ(), 0), [radius, radius + 2, radius + 4])
				.First(point => point != default && geometry.HasLineOfSight(map, point, new BotPosition(npc.GetX(), npc.GetY(), npc.GetZ() + 1, 0)));
			var cleared = instance.GetNpcs().Where(monster => monster.GetObjectId() != npc.GetObjectId() && !monster.IsDead() &&
				NaturalHostility.IsAggressive(monster.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
				MathF.Pow(monster.GetX() - at.X, 2) + MathF.Pow(monster.GetY() - at.Y, 2) < 1600).ToArray();
			foreach (Npc monster in cleared) fixture.World.Despawn(monster);
			if (cleared.Length > 0) log.Add($"cleared at {npc.GetNpcId()}: {string.Join(",", cleared.GroupBy(n => n.GetNpcId()).Select(g => $"{g.Key}x{g.Count()}"))}");
			session.Api.World.BeginWorldReload();
			await TeleportForSetupAsync(session, player, map, at.X, at.Y, at.Z, token);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
		}
		async Task TalkAsync(string key, Npc? chosen = null)
		{
			NaturalAltgardStep step = leg.Steps.Single(step => step.Key == key);
			Npc npc = chosen ?? instance.GetNpcs().First(npc => npc.GetNpcId() == step.NpcId && !npc.IsDead());
			await NearAsync(npc);
			session.BeginStep(key, key);
			log.Add(await NaturalAltgardQuestSteps.TalkAsync(session, step, npc.GetObjectId(), token));
		}
		// Force each approved pool position on the probe, so both dialog approaches are proved independent of the pool draw.
		XElement spawns = XDocument.Load(Path.Combine(RealStaticData.RepoRoot(), "game-server/data/static_data/spawns/Npcs/220030000_Altgard.xml")).Root!;
		XElement spot = spawns.Descendants("spawn").Single(node => (int?)node.Attribute("npc_id") == 203682).Elements("spot").ElementAt(poolSpot);
		foreach (Npc old in instance.GetNpcs().Where(npc => npc.GetNpcId() == 203682).ToArray()) fixture.World.Despawn(old);
		var spawn = SpawnEngine.NewSpawn(map, 203682, (float)spot.Attribute("x")!, (float)spot.Attribute("y")!, (float)spot.Attribute("z")!, 0, 0);
		Npc spirit = Assert.IsType<Npc>(SpawnEngine.SpawnObject(spawn, instance.GetInstanceId()), exactMatch: false);
		await TalkAsync("q2279-offer");
		await TalkAsync("q2279-emgata");
		await TalkAsync("q2279-spirit", spirit);
		Assert.Equal(((byte)3, 2), NaturalAltgardQuestSteps.State(session.Api.World, 2279));
		await TalkAsync("q2279-end");
		Assert.Contains(2279, session.Api.World.CompletedQuestIds);
		log.Add($"Spirit pool spot {poolSpot + 1}: ({spirit.GetX()}, {spirit.GetY()}, {spirit.GetZ()})");
		fixture.World.Despawn(spirit);

		if (poolSpot == 0)
		{
			async Task KillAndLootAsync()
			{
				// Only the local swamp grounds: earlier Fast probes may leave these dead/despawned, so restore a shipped spot on the probe.
				Npc? victim = instance.GetNpcs().Where(npc => npc.GetNpcId() == 210500 && !npc.IsDead())
					.OrderBy(npc => MathF.Pow(npc.GetX() - session.CurrentPosition.X, 2) + MathF.Pow(npc.GetY() - session.CurrentPosition.Y, 2)).FirstOrDefault();
				if (victim == null)
				{
					XElement at = spawns.Descendants("spawn").Single(node => (int?)node.Attribute("npc_id") == 210500).Element("spot")!;
					victim = Assert.IsType<Npc>(SpawnEngine.SpawnObject(SpawnEngine.NewSpawn(map, 210500,
						(float)at.Attribute("x")!, (float)at.Attribute("y")!, (float)at.Attribute("z")!, 0, 0), instance.GetInstanceId()), exactMatch: false);
				}
				await NearAsync(victim, 18);
				victim.GetLifeStats().SetCurrentHp(1);
				Assert.True(await NaturalAirCombat.ShootDownAsync(session, victim.GetObjectId(), 0,
					(origin, skill, level, aim) => runtime.CreateSpellCast(session.Api.World, origin, skill, level, aim), token, maximumCasts: 10));
				bool looted = await NaturalAltgardQuestSteps.LootItemAsync(session, victim.GetObjectId(), pollen, token);
				log.Add($"210500 {(looted ? "dropped pollen" : "no pollen")}; {Items()} held");
			}
			await TalkAsync("q2263-offer-mabrunerk");
			Assert.Equal(300, session.Api.World.Quests[2263].TimerSeconds);
			for (int tries = 0; Items() == 0 && tries < 12; tries++) await KillAndLootAsync();
			Assert.True(Items() > 0);
			// An expected missed timer: let it expire at the safe giver; pollen and START are removed by Java's handler.
			Npc giver = instance.GetNpcs().First(npc => npc.GetNpcId() == 798036);
			await NearAsync(giver);
			await session.AdvanceAsync(TimeSpan.FromSeconds(305), token);
			await session.SynchronizeAsync(token);
			Assert.NotEqual(QuestStatus.START, player.GetQuestStateList().GetQuestState(2263)?.GetStatus());
			Assert.Equal(0, Items());
			log.Add("Q2263 missed its 300 s timer: abandoned, pollen removed (recorded outcome)");
			await TalkAsync("q2263-offer-mabrunerk");
			long began = fixture.Clock.NowMillis;
			for (int tries = 0; Items() < 3 && tries < 20; tries++) await KillAndLootAsync();
			Assert.Equal(3, Items());
			await TalkAsync("q2263-end");
			Assert.Contains(2263, session.Api.World.CompletedQuestIds);
			Assert.Equal(0, Items());
			log.Add($"Q2263 complete {(fixture.Clock.NowMillis - began) / 1000} game s after the offer");
		}
		Console.WriteLine($"AE-03 {string.Join("; ", log)}");
		policy.AssertClean();
	}
}

