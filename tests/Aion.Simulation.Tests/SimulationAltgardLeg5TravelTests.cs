using System.Xml.Linq;
using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// AK-02 (docs/natural-altgard-leveling.md): Leg 5's travel from Basfelt Village. Four round trips from Nokir:
	/// <list type="number">
	/// <item>Brodir and Anmurnerk at Idun's Lake, the grave robbing sentries (Q24230) and fencers (Q24231);</item>
	/// <item>MuMu Village: the looklooks, the herb gatherers, the fertilizer sacks, Chieftain Manumumu and two ring carriers;</item>
	/// <item>Kaibech's Campsite and Gribade Canyon: the plumas, the arachnas and the bigfoot mosbears;</item>
	/// <item>Vovetirn and both sprigg outlaw grounds.</item>
	/// </list>
	/// Each leg is planned with the Altgard travel planner at level 19 and walked on the live server. The report names the
	/// aggressive spawns each plan crosses and the live aggressive monsters within 30 m of each route. The probe is raised to
	/// level 30 (GM setup on a probe: this is about the routes, not the fights), so nothing aggroes and nothing is despawned.
	/// </summary>
	[SkippableFact]
	public async Task AltgardLeg5TravelWalksEveryGroundAndBack()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, level = 19;
		using var policy = NewPolicy("AK02", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l5");
		await using var session = new SimulationL0Session(fixture, policy, "b01", 73, "Asimkaibech", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(player, PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		player.GetCommonData().SetLevel(30);
		var instance = fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance();
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(instance.GetInstanceId(), Race.ASMODIANS);
		BotTravelPlanner planner = BotTravelPlanner.For(altgard, geometry, fixture.DataManager.StaticData)
			?? throw new InvalidDataException("Altgard has no travel planner.");
		BotPosition Ground(float[] at, string what) => geometry.GroundAround(altgard, new BotPosition(at[0], at[1], at[2], 0), [3f, 5f, 8f, 12f])
			.FirstOrDefault() is { } ground && ground != default ? ground : throw new InvalidDataException($"No ground near {what}.");
		XElement spawns = XDocument.Load(Path.Combine(Aion.GameServer.TestKit.RealStaticData.RepoRoot(),
			"game-server/data/static_data/spawns/Npcs/220030000_Altgard.xml")).Root!;
		float[] Nearest(int npcId, float[] to) => spawns.Descendants("spawn").Where(spawn => (int?)spawn.Attribute("npc_id") == npcId)
			.Elements("spot").Select(spot => new[] { (float)spot.Attribute("x")!, (float)spot.Attribute("y")!, (float)spot.Attribute("z")! })
			.OrderBy(at => MathF.Pow(at[0] - to[0], 2) + MathF.Pow(at[1] - to[1], 2)).First();
		float[] nokir = leg.Hub.Anchor;
		float[] brodirAt = Nearest(832821, nokir);
		float[] villageAt = Nearest(210451, nokir);
		float[] kaibechAt = Nearest(203610, nokir);
		float[] vovetirnAt = Nearest(203630, nokir);
		BotPosition basfelt = Ground(nokir, "Nokir");
		BotPosition brodir = Ground(brodirAt, "Brodir");
		BotPosition sentries = Ground(Nearest(210504, brodirAt), "a grave robbing sentry");
		BotPosition fencers = Ground(Nearest(210506, brodirAt), "a grave robbing fencer");
		BotPosition looklooks = Ground(villageAt, "a MuMu looklook");
		BotPosition gatherers = Ground(Nearest(210568, villageAt), "a MuMu herb gatherer");
		BotPosition sacks = Ground(Nearest(700145, villageAt), "a fertilizer sack");
		BotPosition manumumu = Ground(Nearest(210598, villageAt), "Chieftain Manumumu's ground");
		NaturalAltgardTimedSpawn lu = leg.TimedSpawnList.Single(carrier => carrier.NpcId == 210599);
		NaturalAltgardTimedSpawn zoo = leg.TimedSpawnList.Single(carrier => carrier.NpcId == 210621);
		BotPosition luGround = Ground(lu.Position, "MuMu Lu's ground");
		BotPosition zooGround = Ground(zoo.Position, "MuMu Zoo's ground");
		BotPosition kaibech = Ground(kaibechAt, "Kaibech");
		BotPosition plumas = Ground(Nearest(210597, kaibechAt), "a crested pluma");
		BotPosition arachnas = Ground(Nearest(210446, kaibechAt), "a nimble arachna");
		BotPosition bigfoots = Ground(Nearest(210441, kaibechAt), "a bigfoot mosbear");
		BotPosition vovetirn = Ground(vovetirnAt, "Vovetirn");
		BotPosition outlawsNear = Ground(Nearest(210484, vovetirnAt), "a sprigg outlaw (L15)");
		BotPosition outlawsFar = Ground(Nearest(210485, vovetirnAt), "a sprigg outlaw (L16)");
		foreach ((BotPosition at, string key) in new[] { (brodir, "idun-brodir"), (sentries, "sumarhon-sentries"), (fencers, "fencers"),
			(looklooks, "mumu-village"), (manumumu, "mumu-village"), (kaibech, "kaibech-grounds"), (bigfoots, "kaibech-grounds"),
			(vovetirn, "vovetirn-outlaws"), (outlawsFar, "vovetirn-outlaws") })
			Assert.True(leg.Area(key).Contains(at.X, at.Y, at.Z), $"{at} is outside {key}");
		await TeleportForSetupAsync(session, player, altgard, basfelt.X, basfelt.Y, basfelt.Z, token);
		await session.SynchronizeAsync(token);

		var legs = new (string Name, BotPosition To)[]
		{
			("basfelt-to-brodir", brodir), ("brodir-to-sentries", sentries), ("sentries-to-fencers", fencers), ("fencers-to-basfelt", basfelt),
			("basfelt-to-looklooks", looklooks), ("looklooks-to-gatherers", gatherers), ("gatherers-to-sacks", sacks), ("sacks-to-manumumu", manumumu),
			("manumumu-to-mumu-lu", luGround), ("mumu-lu-to-mumu-zoo", zooGround), ("mumu-zoo-to-basfelt", basfelt),
			("basfelt-to-kaibech", kaibech), ("kaibech-to-plumas", plumas), ("plumas-to-arachnas", arachnas), ("arachnas-to-bigfoots", bigfoots),
			("bigfoots-to-basfelt", basfelt),
			("basfelt-to-vovetirn", vovetirn), ("vovetirn-to-outlaws-l15", outlawsNear), ("outlaws-l15-to-outlaws-l16", outlawsFar),
			("outlaws-l16-to-basfelt", basfelt),
		};
		int step = 0;
		foreach ((string name, BotPosition to) in legs)
		{
			session.BeginStep($"s{++step:00}", name);
			// Each leg starts where the last walk ended, as the bot would (a planned ground spot can sit off the navmesh).
			BotPosition from = session.CurrentPosition;
			BotTravelPlan? plan = planner.PlanJourney(altgard, from, to, level, []);
			IReadOnlyList<BotPosition> route = plan?.Route ?? geometry.FindJourneyPath(altgard, from, to);
			Assert.True(route.Count > 0, $"{name}: no route ({BotNavMeshRouter.LastOutcome})");
			bool Near(Aion.GameServer.Model.GameObjects.Npc npc) =>
				NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
				route.Any(point => MathF.Pow(npc.GetX() - point.X, 2) + MathF.Pow(npc.GetY() - point.Y, 2) <= 30 * 30);
			int[] live = instance.GetNpcs().Where(Near).Select(npc => npc.GetNpcId()).ToArray();
			float speed = session.Api.World.MovementSpeed ?? throw new InvalidOperationException("No movement speed.");
			await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateGroundPlan(route, from, speed), token);
			await session.SynchronizeAsync(token);
			float miss = MathF.Sqrt(MathF.Pow(player.GetX() - to.X, 2) + MathF.Pow(player.GetY() - to.Y, 2));
			Assert.True(miss <= 5, $"{name}: ends ({player.GetX()}, {player.GetY()}, {player.GetZ()}), {miss:F1} m from {to}");
			Assert.False(player.IsDead());
			float length = plan?.Length ?? route.Prepend(from).Zip(route, (a, b) => MathF.Sqrt(MathF.Pow(b.X - a.X, 2) + MathF.Pow(b.Y - a.Y, 2))).Sum();
			string danger = plan == null ? "no plan (navmesh route)" : string.Join(", ",
				plan.Danger.GroupBy(d => d.NpcId).Select(group => $"{group.Key}xL{group.First().Level}x{group.Count()}"));
			string near = string.Join(", ", live.GroupBy(id => id).Select(group => $"{group.Key}x{group.Count()}"));
			Console.WriteLine($"AK-02 {name}: {length:F0} m, {route.Count} waypoints, ends z {player.GetZ():F1}; plan crosses [{danger}]; live aggressive within 30 m [{near}]");
			session.TraceDiagnostic("ak02-leg", new Dictionary<string, object?>
			{
				["leg"] = name, ["metres"] = length, ["waypoints"] = route.Count, ["planned"] = plan != null, ["danger"] = danger, ["liveAggressive"] = live.Length,
			});
		}
		policy.AssertClean();
	}
}
