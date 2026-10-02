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
	/// AG-02 (docs/natural-altgard-leveling.md): Leg 6's travel from the Trader's Berth obelisk. Three round trips:
	/// <list type="number">
	/// <item>Gerger Village and its grounds: Gemyu, the green ribbits (Q2244), the insignia box (Q2246), the first disguised
	/// Germir (Q2284), Gogaerunerk (Q2247) and the star metal ksellids (Q2245);</item>
	/// <item>the blackened angolems (Q2249), the second disguised Germir, and Q2284's escort line from him to Babarunerk;</item>
	/// <item>the peckus (Q2251), both crimsontail grounds (Q24115) and the Bones of Minushan (Q2252).</item>
	/// </list>
	/// Each leg is planned with the Altgard travel planner at level 20 and walked on the live server. The report names the
	/// aggressive spawns each plan crosses and the live aggressive monsters within 30 m of each route. The probe is raised to
	/// level 30 (GM setup on a probe: this is about the routes, not the fights), so nothing aggroes and nothing is despawned.
	/// </summary>
	[SkippableFact]
	public async Task AltgardLeg6TravelWalksEveryGroundAndBack()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, level = 20;
		using var policy = NewPolicy("AG02", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l6");
		await using var session = new SimulationL0Session(fixture, policy, "b01", 67, "Asimberth", Race.ASMODIANS);
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
		float[] obelisk = leg.Bind!.Position;
		float[] gemyuAt = Nearest(203643, obelisk);
		BotPosition berth = Ground(obelisk, "the Trader's Berth obelisk");
		BotPosition gemyu = Ground(gemyuAt, "Gemyu");
		BotPosition ribbits = Ground(Nearest(210573, gemyuAt), "a green ribbit");
		BotPosition insignia = Ground(Nearest(700147, gemyuAt), "the insignia box");
		BotPosition firstGermir = Ground(Nearest(798040, gemyuAt), "the first disguised Germir");
		BotPosition gogaerunerk = Ground(Nearest(798039, gemyuAt), "Gogaerunerk");
		BotPosition ksellids = Ground(Nearest(210490, gemyuAt), "a star metal ksellid");
		BotPosition angolems = Ground(Nearest(210487, obelisk), "a blackened angolem");
		BotPosition secondGermir = Ground(Nearest(798041, obelisk), "the second disguised Germir");
		BotPosition babarunerk = Ground(Nearest(798034, obelisk), "Babarunerk");
		BotPosition peckus = Ground(Nearest(212345, obelisk), "a pecku");
		BotPosition crimsontailsNear = Ground(Nearest(210493, obelisk), "a crimsontail ampha (L17)");
		BotPosition crimsontailsFar = Ground(Nearest(210492, obelisk), "a crimsontail ampha (L18)");
		BotPosition bones = Ground(Nearest(700060, obelisk), "the Bones of Minushan");
		foreach ((BotPosition at, string key) in new[] { (berth, "berth"), (gemyu, "gerger"), (ribbits, "ribbits"), (insignia, "insignia-box"),
			(firstGermir, "disguised-germir"), (gogaerunerk, "gogaerunerk"), (ksellids, "ksellids"), (angolems, "angolems"),
			(secondGermir, "escort-line"), (babarunerk, "escort-line"), (peckus, "peckus"), (crimsontailsNear, "crimsontails"),
			(crimsontailsFar, "crimsontails"), (bones, "minushan") })
			Assert.True(leg.Area(key).Contains(at.X, at.Y, at.Z), $"{at} is outside {key}");
		await TeleportForSetupAsync(session, player, altgard, berth.X, berth.Y, berth.Z, token);
		await session.SynchronizeAsync(token);

		var legs = new (string Name, BotPosition To)[]
		{
			("berth-to-gemyu", gemyu), ("gemyu-to-ribbits", ribbits), ("ribbits-to-insignia-box", insignia), ("insignia-box-to-first-germir", firstGermir),
			("first-germir-to-gogaerunerk", gogaerunerk), ("gogaerunerk-to-ksellids", ksellids), ("ksellids-to-berth", berth),
			("berth-to-angolems", angolems), ("angolems-to-second-germir", secondGermir), ("escort-line-to-babarunerk", babarunerk),
			("babarunerk-to-berth", berth),
			("berth-to-peckus", peckus), ("peckus-to-crimsontails-l17", crimsontailsNear), ("crimsontails-l17-to-l18", crimsontailsFar),
			("crimsontails-l18-to-bones", bones), ("bones-to-berth", berth),
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
			Console.WriteLine($"AG-02 {name}: {length:F0} m, {route.Count} waypoints, ends z {player.GetZ():F1}; plan crosses [{danger}]; live aggressive within 30 m [{near}]");
			session.TraceDiagnostic("ag02-leg", new Dictionary<string, object?>
			{
				["leg"] = name, ["metres"] = length, ["waypoints"] = route.Count, ["planned"] = plan != null, ["danger"] = danger, ["liveAggressive"] = live.Length,
			});
		}
		policy.AssertClean();
	}
}
