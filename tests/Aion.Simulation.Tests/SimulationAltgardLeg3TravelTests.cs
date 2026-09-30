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
	/// AC-02 (docs/natural-altgard-leveling.md): Leg 3's travel. From Manir the bot walks to Groken's Safe, to Groken, along
	/// the escort line to the goal stand beside Groken's boat, and back to Manir; then to Karl, to Nokir at Basfelt, and back.
	/// Each leg is planned with the Altgard travel planner at level 15 and walked on the live server. The report names the
	/// aggressive spawns each plan crosses and the live aggressive monsters within 30 m of each route; those are despawned
	/// before the walk (GM setup on a probe character: this is about the routes, not the fights). The escort line is also cut
	/// into the escort policy's hops (at most 10 m), and the dock at z 247 must be on the navmesh.
	/// </summary>
	[SkippableFact]
	public async Task AltgardManirTravelWalksTheEscortLineAndToBasfelt()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, level = 15;
		const float hop = 10, standFromGoal = 10;
		using var policy = NewPolicy("AC02", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l3");
		NaturalAltgardEscort escort = leg.EscortList.Single();
		await using var session = new SimulationL0Session(fixture, policy, "b01", 143, "Asimmanir", Race.ASMODIANS);
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
		float[] StepAt(string key) => leg.Steps.Single(step => step.Key == key).Position;

		// The dock is on the navmesh at the boat's height.
		BotPosition boat = geometry.SnapToGround(altgard, new BotPosition(escort.Goal[0], escort.Goal[1], escort.Goal[2], 0))
			?? throw new InvalidDataException("No navmesh ground at Groken's boat.");
		Assert.InRange(boat.Z, escort.Goal[2] - 3, escort.Goal[2] + 3);
		BotPosition manir = Ground(leg.Hub.Anchor, "Manir");
		BotPosition safe = Ground([1266.44f, 1109.84f, 254.125f], "Groken's Safe");
		Assert.True(leg.Area(leg.ObjectUseList.Single().Area!).Contains(1266.44f, 1109.84f, 254.125f));
		BotPosition groken = Ground(StepAt(escort.RestartStep), "Groken");
		// The goal stand: on Groken's side of the boat, standFromGoal m out, so a trailing Groken ends inside the radius.
		float dx = groken.X - escort.Goal[0], dy = groken.Y - escort.Goal[1], d = MathF.Sqrt(dx * dx + dy * dy);
		BotPosition stand = Ground([escort.Goal[0] + dx / d * standFromGoal, escort.Goal[1] + dy / d * standFromGoal, escort.Goal[2]], "the goal stand");
		Assert.True(MathF.Sqrt(MathF.Pow(stand.X - escort.Goal[0], 2) + MathF.Pow(stand.Y - escort.Goal[1], 2)) <= escort.GoalRadius - 5);
		BotPosition karl = Ground(StepAt("q2222-v0-karl"), "Karl");
		BotPosition nokir = Ground(StepAt("q2222-v1-nokir"), "Nokir");
		await TeleportForSetupAsync(session, player, altgard, manir.X, manir.Y, manir.Z, token);
		await session.SynchronizeAsync(token);

		var legs = new (string Name, BotPosition From, BotPosition To)[]
		{
			("manir-to-safe", manir, safe), ("safe-to-groken", safe, groken), ("escort-groken-to-goal-stand", groken, stand),
			("goal-stand-to-manir", stand, manir), ("manir-to-karl", manir, karl), ("karl-to-nokir", karl, nokir),
			("nokir-to-karl", nokir, karl), ("karl-to-manir", karl, manir),
		};
		int step = 0;
		foreach ((string name, BotPosition from, BotPosition to) in legs)
		{
			session.BeginStep($"s{++step:00}", name);
			BotTravelPlan? plan = planner.PlanJourney(altgard, from, to, level, []);
			IReadOnlyList<BotPosition> route = plan?.Route ?? geometry.FindJourneyPath(altgard, from, to);
			Assert.True(route.Count > 0, $"{name}: no route ({BotNavMeshRouter.LastOutcome})");
			bool Near(Aion.GameServer.Model.GameObjects.Npc npc) =>
				NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
				route.Any(point => MathF.Pow(npc.GetX() - point.X, 2) + MathF.Pow(npc.GetY() - point.Y, 2) <= 30 * 30);
			int[] live = instance.GetNpcs().Where(Near).Select(npc => npc.GetNpcId()).ToArray();
			foreach (var npc in instance.GetNpcs().Where(Near).ToArray())
				fixture.World.Despawn(npc);
			float speed = session.Api.World.MovementSpeed ?? throw new InvalidOperationException("No movement speed.");
			await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateGroundPlan(route, from, speed), token);
			await session.SynchronizeAsync(token);
			float miss = MathF.Sqrt(MathF.Pow(player.GetX() - to.X, 2) + MathF.Pow(player.GetY() - to.Y, 2));
			Assert.True(miss <= 5, $"{name}: ends ({player.GetX()}, {player.GetY()}, {player.GetZ()}), {miss:F1} m from {to}");
			Assert.False(player.IsDead());
			float[] segments = route.Prepend(from).Zip(route, (a, b) => MathF.Sqrt(MathF.Pow(b.X - a.X, 2) + MathF.Pow(b.Y - a.Y, 2))).ToArray();
			float length = plan?.Length ?? segments.Sum();
			int hops = segments.Sum(segment => (int)MathF.Ceiling(segment / hop));
			string danger = plan == null ? "no plan (navmesh route)" : string.Join(", ",
				plan.Danger.GroupBy(dg => dg.NpcId).Select(group => $"{group.Key}xL{group.First().Level}x{group.Count()}"));
			string near = string.Join(", ", live.GroupBy(id => id).Select(group => $"{group.Key}x{group.Count()}"));
			Console.WriteLine($"AC-02 {name}: {length:F0} m, {route.Count} waypoints, longest segment {segments.Max():F1} m, {hops} hops of <= {hop} m; " +
				$"plan crosses [{danger}]; live aggressive within 30 m [{near}]");
			session.TraceDiagnostic("ac02-leg", new Dictionary<string, object?>
			{
				["leg"] = name, ["metres"] = length, ["waypoints"] = route.Count, ["planned"] = plan != null, ["longestSegment"] = segments.Max(),
				["hops"] = hops, ["danger"] = danger, ["liveAggressive"] = live.Length,
			});
		}
		Console.WriteLine($"AC-02 dock navmesh z {boat.Z:F2} (boat {escort.Goal[2]}); goal stand ({stand.X:F1}, {stand.Y:F1}, {stand.Z:F1})");
		policy.AssertClean();
	}
}
