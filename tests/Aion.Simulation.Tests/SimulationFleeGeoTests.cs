using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World.Geo;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// <c>flee_from</c> runs over the ground, not at the height it started from.
	/// </summary>
	/// <remarks>
	/// From <c>ak08-l5-smoke2</c> (step <c>ni07-q24230-2-Kill</c>): a grave robbing sentry (210504) at 11% HP fled from the
	/// natural Cleric up the slope west of Sumarhon's camp, from z 284.67 at (1497.66, 464.38). <c>FleeFrom</c> aimed it at
	/// that start height, so its SM_MOVE carried targetZ 284.67, and the move controller's once-a-second ground clamp
	/// (searching only 2 m above the interpolated height) lost the terrain as it rose to 288. The sentry stopped at z 285.18,
	/// 2.8 m inside the hill under the bot's feet, and every cast at it failed with STR_SKILL_OBSTACLE. The flee now takes its
	/// end point from <c>GeoService.FindMovementCollision</c>, as Java's FearTask and ConfuseTask do.
	/// GM setup only: a fresh sentry at the flee start, the probe beside it as its target, and the flee called directly.
	/// </remarks>
	[SkippableFact]
	public async Task FleeingUphillEndsOnTheGroundInSight()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000;
		using var policy = NewPolicy("FLEE-GEO", includeHistory: true);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(fixture, policy, "b01", 69, "Asimflee", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(player, PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		player.GetCommonData().SetLevel(19);
		var instance = fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance();
		await TeleportForSetupAsync(session, player, altgard, 1499.76f, 465.25f, 284.66f, token);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);

		var spawned = Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(new SpawnTemplate(new SpawnGroup(altgard, 210504, 0, null),
			1497.6562f, 464.37634f, 284.67206f, 67, 0, null, 0), instance.GetInstanceId());
		var sentry = Assert.IsType<Npc>(spawned);
		try
		{
			var ai = Assert.IsAssignableFrom<PatternAi>(sentry.GetAi());
			// In the trace the sentry fled mid-fight, at its combat run speed: start the fight, then stop whatever move it
			// had going, since a flee is a point move and the move controller refuses one while another kind is running.
			sentry.GetAggroList().AddHate(player, 1);
			await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
			Assert.True(sentry.IsInState(Aion.GameServer.Model.GameObjects.State.CreatureState.WEAPON_EQUIPPED), "The sentry did not start fighting.");
			sentry.GetMoveController().AbortMove();
			sentry.SetTarget(player);
			session.BeginStep("s01", "flee-uphill");
			int packetStart = session.PacketHistory.Count;
			string fleeStart = $"sentry at ({sentry.GetX()}, {sentry.GetY()}, {sentry.GetZ()}), player at ({player.GetX()}, {player.GetY()}, " +
				$"{player.GetZ()}), speed {sentry.GetGameStats().GetMovementSpeedFloat()}";
			ai.Flee(8);
			Assert.NotNull(ai.FleeingTo);
			// Read where it stopped as the flee ends, before on_stop_to_flee turns it back to the fight.
			for (int tick = 0; tick < 120 && ai.FleeingTo != null; tick++)
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(100), token);
			Assert.Null(ai.FleeingTo);
			DateTimeOffset fleeEnded = Aion.GameServer.Utils.SystemClock.UtcNow();
			(float X, float Y, float Z) stop = (sentry.GetX(), sentry.GetY(), sentry.GetZ());
			// Sight between bodies, taken now, before it turns back to the fight: from the sentry where it stopped to the ground
			// 6 m back along its run, where a player chasing it stands. A sentry inside the hill (the bug) is hidden even from
			// there. The earlier check, a ray from the trace's spot 15 m away to its feet, grazed the hill's crest, so a stop a
			// tenth of a metre further (as other Fast tests' clock left it) flipped the result.
			float back = MathF.Sqrt(MathF.Pow(1497.6562f - stop.X, 2) + MathF.Pow(464.37634f - stop.Y, 2));
			float behindX = stop.X + (1497.6562f - stop.X) / back * 6, behindY = stop.Y + (464.37634f - stop.Y) / back * 6;
			float behindZ = GeoService.GetInstance().GetZ(altgard, behindX, behindY, stop.Z + 10, stop.Z - 10, instance.GetInstanceId());
			bool inSight = GeoService.GetInstance().CanSee(sentry, behindX, behindY, behindZ,
				Aion.GameServer.GeoEngine.Collision.IgnoreProperties.ANY_RACE);
			await session.SynchronizeAsync(token);

			// The flee's own moves: once it ends, on_stop_to_flee turns the sentry back to the fight, and its next move aims at the
			// player (run-fast, where the shared world's clock let one arrive before the read).
			var aimed = session.PacketHistory.Skip(packetStart).Where(p => p.PacketType == typeof(SM_MOVE)
				&& p.Get<int>("objectId") == sentry.GetObjectId() && p.Fields.ContainsKey("targetZ")
				&& (p.ReceivedAt is not DateTimeOffset at || at <= fleeEnded)).ToList();
			Assert.NotEmpty(aimed);
			float targetX = aimed[^1].Get<float>("targetX"), targetY = aimed[^1].Get<float>("targetY"), targetZ = aimed[^1].Get<float>("targetZ");
			float groundAtTarget = GeoService.GetInstance().GetZ(altgard, targetX, targetY, targetZ + 10, targetZ - 10, instance.GetInstanceId());
			Assert.True(MathF.Abs(targetZ - groundAtTarget) < 0.5f, $"Flee aimed at z {targetZ}; the ground there is {groundAtTarget}.");
			Assert.True(targetX < 1480, $"The flee should still run up the slope to the west; it aimed at ({targetX}, {targetY}) from {fleeStart}.");

			float ground = GeoService.GetInstance().GetZ(altgard, stop.X, stop.Y, stop.Z + 10, stop.Z - 10, instance.GetInstanceId());
			Assert.True(MathF.Abs(stop.Z - ground) < 0.75f, $"The sentry stopped at {stop}; the ground there is {ground}.");
			Assert.True(inSight, $"The sentry, stopped at {stop}, is out of sight of the ground 6 m back ({behindX}, {behindY}, {behindZ}).");
			policy.AssertClean();
		}
		finally
		{
			if (!sentry.IsDead())
				fixture.World.Despawn(sentry);
		}
	}
}
