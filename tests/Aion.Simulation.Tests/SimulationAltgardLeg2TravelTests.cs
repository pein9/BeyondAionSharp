using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// AM-02 (docs/natural-altgard-leveling.md): Leg 2's travel. From beside the fortress obelisk the bot walks to Moslan
	/// Crossroad, then from the crossroad to each ground (the Okaru Tree, the karnif slopes, the MuMu Farmland, the tog
	/// grounds and Manir's Campsite) and back, and finally home. Each leg is planned the way the journey plans a long leg,
	/// with the Altgard travel planner at level 13, and walked on the live server. The report names the aggressive spawns
	/// each plan crosses and the live aggressive monsters within 30 m of each route; those are despawned before the walk
	/// (GM setup: this probe is about the routes, not the fights, which are AM-03 and AM-06).
	/// </summary>
	[SkippableFact]
	public async Task AltgardMoslanTravelWalksEveryGroundAndBack()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, level = 13;
		using var policy = NewPolicy("AM02", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l2");
		IReadOnlyDictionary<int, QuestRunPlan> plans = NaturalAltgardContract.LoadPlans("l2");
		await using var session = new SimulationL0Session(fixture, policy, "b01", 139, "Asimmoslan", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		var instance = fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance();
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(instance.GetInstanceId(), Race.ASMODIANS);
		BotTravelPlanner planner = BotTravelPlanner.For(altgard, geometry, fixture.DataManager.StaticData)
			?? throw new InvalidDataException("Altgard has no travel planner.");
		BotPosition Ground(float[] at, string what) => geometry.GroundAround(altgard, new BotPosition(at[0], at[1], at[2], 0), [3f, 5f, 8f])
			.FirstOrDefault() is { } ground && ground != default ? ground : throw new InvalidDataException($"No ground near {what}.");
		BotPosition town = Ground(leg.Town!.Anchor, "the obelisk");
		BotPosition crossroad = Ground(leg.Hub.Anchor, "the crossroad");
		QuestRunPosition Nearest(int quest, string kind) => plans[quest].Steps.Where(step => step.Kind == kind)
			.SelectMany(step => step.Npcs.Concat(step.Sources.Select(source => source.Npc).OfType<QuestRunNpc>()))
			.SelectMany(npc => npc.Positions).Where(at => at.MapId == altgard)
			.OrderBy(at => MathF.Pow(at.X - crossroad.X, 2) + MathF.Pow(at.Y - crossroad.Y, 2)).First();
		QuestRunPosition karnif = Nearest(2211, "kill"), tog = Nearest(2212, "collect"), patrol = Nearest(2220, "kill");
		NaturalAltgardObjectUse tree = leg.ObjectUseList.Single(use => use.QuestId == 2213);
		float[] treeAt = [1412.96f, 1441.74f, 282.495f];
		Assert.True(leg.Area(tree.Area!).Contains(treeAt[0], treeAt[1], treeAt[2]));
		var grounds = new (string Name, BotPosition At)[]
		{
			("okaru-tree", Ground(treeAt, "the Okaru Tree")),
			("karnif-slopes", Ground([karnif.X, karnif.Y, karnif.Z], "a karnif")),
			("farmland", Ground([patrol.X, patrol.Y, patrol.Z], "a MuMu patrol")),
			("tog-grounds", Ground([tog.X, tog.Y, tog.Z], "a wild tog")),
			("manirs-campsite", Ground(leg.Endpoint.Anchor!, "Manir")),
		};
		await TeleportForSetupAsync(session, player, altgard, town.X, town.Y, town.Z, token);
		await session.SynchronizeAsync(token);

		var legs = new List<(string Name, BotPosition From, BotPosition To)> { ("town-to-crossroad", town, crossroad) };
		foreach ((string name, BotPosition at) in grounds)
		{
			legs.Add(($"crossroad-to-{name}", crossroad, at));
			legs.Add(($"{name}-to-crossroad", at, crossroad));
		}
		legs.Add(("crossroad-to-town", crossroad, town));
		int step = 0;
		foreach ((string name, BotPosition from, BotPosition to) in legs)
		{
			session.BeginStep($"s{++step:00}", name);
			BotTravelPlan? plan = planner.PlanJourney(altgard, from, to, level, []);
			IReadOnlyList<BotPosition> route = plan?.Route ?? geometry.FindJourneyPath(altgard, from, to);
			Assert.True(route.Count > 0, $"{name}: no route ({BotNavMeshRouter.LastOutcome})");
			int[] live = instance.GetNpcs().Where(npc =>
				NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
				route.Any(point => MathF.Pow(npc.GetX() - point.X, 2) + MathF.Pow(npc.GetY() - point.Y, 2) <= 30 * 30))
				.Select(npc => npc.GetNpcId()).ToArray();
			foreach (var npc in instance.GetNpcs().Where(npc =>
				NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
				route.Any(point => MathF.Pow(npc.GetX() - point.X, 2) + MathF.Pow(npc.GetY() - point.Y, 2) <= 30 * 30)).ToArray())
				fixture.World.Despawn(npc);
			float speed = session.Api.World.MovementSpeed ?? throw new InvalidOperationException("No movement speed.");
			await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateGroundPlan(route, from, speed), token);
			await session.SynchronizeAsync(token);
			float miss = MathF.Sqrt(MathF.Pow(player.GetX() - to.X, 2) + MathF.Pow(player.GetY() - to.Y, 2));
			Assert.True(miss <= 5, $"{name}: ends ({player.GetX()}, {player.GetY()}, {player.GetZ()}), {miss:F1} m from {to}");
			Assert.False(player.IsDead());
			string danger = plan == null ? "no plan (navmesh route)" : string.Join(", ",
				plan.Danger.GroupBy(d => d.NpcId).Select(group => $"{group.Key}xL{group.First().Level}x{group.Count()}"));
			string near = string.Join(", ", live.GroupBy(id => id).Select(group => $"{group.Key}x{group.Count()}"));
			float length = plan?.Length ?? route.Zip(route.Skip(1), (a, b) => MathF.Sqrt(MathF.Pow(b.X - a.X, 2) + MathF.Pow(b.Y - a.Y, 2))).Sum();
			Console.WriteLine($"AM-02 {name}: {length:F0} m, {route.Count} waypoints; plan crosses [{danger}]; live aggressive within 30 m [{near}]");
			session.TraceDiagnostic("am02-leg", new Dictionary<string, object?>
			{
				["leg"] = name, ["metres"] = length, ["waypoints"] = route.Count, ["planned"] = plan != null,
				["danger"] = danger, ["liveAggressive"] = live.Length,
			});
		}
		policy.AssertClean();
	}
}
