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
	/// AB-02 (docs/natural-altgard-leveling.md): Leg 4's travel from Basfelt Village. Five round trips from Nokir:
	/// <list type="number">
	/// <item>Gornak and the south mosbears;</item>
	/// <item>Vovetirn and the amphas;</item>
	/// <item>the west mosbears and the incense burner;</item>
	/// <item>Karl, Gunmarson, the beehive grove, Komu's ground and Kaibech;</item>
	/// <item>Brodir, Sumarhon's camp on its height (z 333) and the Q24013 black claw ground.</item>
	/// </list>
	/// Each leg is planned with the Altgard travel planner at level 16 and walked on the live server. The report names the
	/// aggressive spawns each plan crosses and the live aggressive monsters within 30 m of each route. The probe character
	/// is raised to level 30 (GM setup on a probe: this is about the routes, not the fights), so Java's aggro rule (a monster
	/// attacks only players fewer than 10 levels above it) leaves it alone and nothing is despawned: the shared Fast world
	/// keeps Komu Silverclaw (hourly respawn) and Comrade Sumarhon for later probes.
	/// </summary>
	[SkippableFact]
	public async Task AltgardBasfeltTravelWalksEveryGroundAndBack()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, level = 16;
		using var policy = NewPolicy("AB02", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l4");
		await using var session = new SimulationL0Session(fixture, policy, "b01", 149, "Asimbasfelt", Race.ASMODIANS);
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
		float[] StepAt(string key) => leg.Steps.Single(step => step.Key == key).Position;
		XElement spawns = XDocument.Load(Path.Combine(Aion.GameServer.TestKit.RealStaticData.RepoRoot(),
			"game-server/data/static_data/spawns/Npcs/220030000_Altgard.xml")).Root!;
		float[] Nearest(int npcId, float[] to) => spawns.Descendants("spawn").Where(spawn => (int?)spawn.Attribute("npc_id") == npcId)
			.Elements("spot").Select(spot => new[] { (float)spot.Attribute("x")!, (float)spot.Attribute("y")!, (float)spot.Attribute("z")! })
			.OrderBy(at => MathF.Pow(at[0] - to[0], 2) + MathF.Pow(at[1] - to[1], 2)).First();
		float[] nokir = leg.Hub.Anchor;
		BotPosition basfelt = Ground(nokir, "Nokir");
		BotPosition gornak = Ground(Nearest(203639, nokir), "Gornak");
		BotPosition starved = Ground(Nearest(210564, nokir), "a starved mosbear");
		BotPosition vovetirn = Ground(StepAt("q2239-v0-vovetirn"), "Vovetirn");
		BotPosition ampha = Ground(Nearest(210482, StepAt("q2239-v0-vovetirn")), "a poisonsac ampha");
		BotPosition swamp = Ground(Nearest(210436, nokir), "a swamp mosbear");
		NaturalAltgardSpawn infernus = leg.SpawnList.Single();
		BotPosition burner = Ground(Nearest(infernus.TriggerNpcId, nokir), "the incense burner");
		BotPosition karl = Ground(StepAt("q2231-v0-karl"), "Karl");
		BotPosition gunmarson = Ground(StepAt("q2231-v1-gunmarson"), "Gunmarson");
		BotPosition hive = Ground(Nearest(leg.ObjectUseList.Single().NpcId, StepAt("q2231-v1-gunmarson")), "a beehive");
		BotPosition komu = Ground(Nearest(leg.AvoidList.Single().NpcId, nokir), "Komu's ground");
		BotPosition kaibech = Ground(StepAt("q2231-v2-kaibech"), "Kaibech");
		BotPosition brodir = Ground(StepAt("q24112-v1-brodir"), "Brodir");
		BotPosition sumarhon = Ground(Nearest(210510, nokir), "Sumarhon's camp");
		NaturalAltgardArea claws = leg.Area("black-claws");
		BotPosition clawGround = Ground([1675f, 234f, 290f], "the Q24013 zone");
		Assert.True(claws.Contains(clawGround.X, clawGround.Y, clawGround.Z));
		await TeleportForSetupAsync(session, player, altgard, basfelt.X, basfelt.Y, basfelt.Z, token);
		await session.SynchronizeAsync(token);

		var legs = new (string Name, BotPosition From, BotPosition To)[]
		{
			("basfelt-to-gornak", basfelt, gornak), ("gornak-to-starved-mosbears", gornak, starved), ("starved-mosbears-to-basfelt", starved, basfelt),
			("basfelt-to-vovetirn", basfelt, vovetirn), ("vovetirn-to-amphas", vovetirn, ampha), ("amphas-to-basfelt", ampha, basfelt),
			("basfelt-to-swamp-mosbears", basfelt, swamp), ("swamp-mosbears-to-burner", swamp, burner), ("burner-to-basfelt", burner, basfelt),
			("basfelt-to-karl", basfelt, karl), ("karl-to-gunmarson", karl, gunmarson), ("gunmarson-to-beehives", gunmarson, hive),
			("beehives-to-komu", hive, komu), ("komu-to-kaibech", komu, kaibech), ("kaibech-to-basfelt", kaibech, basfelt),
			("basfelt-to-brodir", basfelt, brodir), ("brodir-to-sumarhon-camp", brodir, sumarhon), ("sumarhon-camp-to-black-claws", sumarhon, clawGround),
			("black-claws-to-basfelt", clawGround, basfelt),
		};
		int step = 0;
		foreach ((string name, BotPosition planned, BotPosition to) in legs)
		{
			session.BeginStep($"s{++step:00}", name);
			// Each leg starts where the last walk ended, as the bot would (a planned ground spot can sit off the navmesh).
			BotPosition from = step == 1 ? planned : session.CurrentPosition;
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
			Console.WriteLine($"AB-02 {name}: {length:F0} m, {route.Count} waypoints, ends z {player.GetZ():F1}; plan crosses [{danger}]; live aggressive within 30 m [{near}]");
			session.TraceDiagnostic("ab02-leg", new Dictionary<string, object?>
			{
				["leg"] = name, ["metres"] = length, ["waypoints"] = route.Count, ["planned"] = plan != null, ["danger"] = danger, ["liveAggressive"] = live.Length,
			});
		}
		policy.AssertClean();
	}
}
