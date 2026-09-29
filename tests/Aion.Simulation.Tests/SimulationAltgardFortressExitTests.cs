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
	/// AF-02 (docs/natural-altgard-leveling.md): the bot leaves Altgard Fortress on foot and comes back. From ground beside
	/// the obelisk it routes on the live server's geometry to the nearest spawn of every Leg 1 hunt target on the Ice Lake,
	/// walks there, and walks back. Aggressive monsters near each route are despawned first: this probe is about the route,
	/// not combat, and that setup is GM preparation like NA-23's, never natural play.
	/// </summary>
	[SkippableFact]
	public async Task AltgardFortressExitWalksToTheIceLakeTargetsAndBack()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000;
		using var policy = NewPolicy("AF02", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract contract = NaturalAltgardContract.LoadDefault();
		await using var session = new SimulationL0Session(fixture, policy, "b01", 62, "Asimexit", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		BotNavigationGeometry Geometry() => BotNavigationGeometry.ForServerWorld(player.GetInstanceId(), Race.ASMODIANS);
		// The obelisk itself is solid (NA-04 finding b), so the walk starts 3 m west of it, where a player binds.
		BotPosition obelisk = BotNavigationGeometry.ForServerWorld(fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance().GetInstanceId(), Race.ASMODIANS).SnapToGround(altgard,
			new BotPosition(contract.Hub.Anchor[0] - 3, contract.Hub.Anchor[1], contract.Hub.Anchor[2] + 1, 0))
			?? throw new InvalidDataException("No ground beside the Altgard obelisk.");
		await TeleportForSetupAsync(session, player, altgard, obelisk.X, obelisk.Y, obelisk.Z, token);
		await session.SynchronizeAsync(token);
		BotNavigationGeometry geometry = Geometry();

		// One destination per hunt target: its spawn nearest the obelisk, on the Ice Lake.
		NaturalAltgardArea lake = contract.Area("ice-lake");
		var destinations = NaturalAltgardContract.LoadPlans().Values.OrderBy(plan => plan.Id)
			.SelectMany(plan => plan.Steps.Where(step => step.Kind is "kill" or "collect")
				.SelectMany(step => step.Npcs.Concat(step.Sources.Select(source => source.Npc).OfType<QuestRunNpc>()))
				.Select(npc => (Quest: plan.Id, Npc: npc.Id, At: npc.Positions
					.Where(at => at.MapId == altgard && lake.Contains(at.X, at.Y, at.Z))
					.OrderBy(at => MathF.Pow(at.X - obelisk.X, 2) + MathF.Pow(at.Y - obelisk.Y, 2)).First())))
			.DistinctBy(target => target.Npc).ToArray();
		Assert.NotEmpty(destinations);

		int leg = 0;
		foreach ((int quest, int npc, QuestRunPosition at) in destinations)
		{
			BotPosition target = geometry.GroundAround(altgard, new BotPosition(at.X, at.Y, at.Z, 0), [4f, 6f, 8f])
				.FirstOrDefault(point => geometry.FindJourneyPath(altgard, obelisk, point).Count > 0);
			Assert.True(target != default, $"Q{quest} target {npc} at ({at.X}, {at.Y}, {at.Z}): no routed ground within 8 m ({BotNavMeshRouter.LastOutcome})");
			foreach ((string direction, BotPosition from, BotPosition to) in new[] { ("out", obelisk, target), ("back", target, obelisk) })
			{
				leg++;
				session.BeginStep($"s{leg:00}", $"walk-{direction}-q{quest}-{npc}");
				IReadOnlyList<BotPosition> route = geometry.FindJourneyPath(altgard, from, to);
				Assert.True(route.Count > 0, $"Q{quest} {direction}: {from} -> {to} {BotNavMeshRouter.LastOutcome}");
				ClearAggressiveNear(route);
				float speed = session.Api.World.MovementSpeed ?? throw new InvalidOperationException("No movement speed.");
				await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateGroundPlan(route, from, speed), token);
				await session.SynchronizeAsync(token);
				float miss = MathF.Sqrt(MathF.Pow(player.GetX() - to.X, 2) + MathF.Pow(player.GetY() - to.Y, 2));
				Assert.True(miss <= 3, $"Q{quest} {direction}: the server has the player at ({player.GetX()}, {player.GetY()}, {player.GetZ()}), {miss:F1} m from {to}");
				Assert.False(player.IsDead(), $"Q{quest} {direction}: died on the way");
				session.TraceDiagnostic("af02-leg", new Dictionary<string, object?>
				{
					["quest"] = quest, ["npc"] = npc, ["direction"] = direction, ["waypoints"] = route.Count,
					["x"] = player.GetX(), ["y"] = player.GetY(), ["z"] = player.GetZ(),
				});
				Console.WriteLine($"AF-02 leg {leg}: Q{quest} {npc} {direction} {route.Count} waypoints, ends ({player.GetX():F1}, {player.GetY():F1}, {player.GetZ():F1})");
			}
		}
		policy.AssertClean();

		void ClearAggressiveNear(IReadOnlyList<BotPosition> route)
		{
			var instance = fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance();
			foreach (var npc in instance.GetNpcs().Where(npc =>
				NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
				route.Any(point => MathF.Pow(npc.GetX() - point.X, 2) + MathF.Pow(npc.GetY() - point.Y, 2) <= 30 * 30)).ToArray())
				fixture.World.Despawn(npc);
		}
	}
}
