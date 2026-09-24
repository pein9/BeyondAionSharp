using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	[SkippableFact]
	public async Task FreshPriestCanCastLearnedReturnToNaturalBindLocation()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		using var policy = NewPolicy("NI07-RETURN", includeHistory: true);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(fixture, policy, "b01", 94,
			"Asmreturnni", Race.ASMODIANS);
		session.BeginStep("return-probe", "ordinary-priest-return-skill");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
		await session.SynchronizeAsync(token);
		Assert.True(session.Api.World.Skills.TryGetValue(243, out BotSkill? learned));
		int packetStart = session.PacketHistory.Count;
		await session.SendPacketAsync(session.Api.Target(session.CharacterId), token);
		await session.SendPacketAsync(session.Api.Cast(new SpellCastData(243,
			checked((byte)learned!.Level), 0) { TargetObjectId = session.CharacterId }), token);
		DecodedBotServerPacket started = await BotCastProtocol.WaitForStartAsync(
			(predicate, waitToken) => session.WaitForPacketAsync(packet => predicate(packet) ||
				BotCastProtocol.IsStartRejection(packet), waitToken), session.CharacterId, 243, token);
		Assert.Equal(typeof(SM_CASTSPELL), started.PacketType);
		session.Api.World.BeginWorldReload();
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(started.Get<ushort>("castDuration") + 1), token);
		DecodedBotServerPacket result = await BotCastProtocol.WaitForCompletionAsync(
			session.WaitForPacketAsync, session.CharacterId, 243, token);
		Assert.Equal(typeof(SM_CASTSPELL_RESULT), result.PacketType);
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(result.Get<ushort>("hitTime") + 1), token);
		await session.SynchronizeAsync(token);
		Assert.True(session.PacketHistory.Skip(packetStart).Any(packet =>
			packet.PacketType == typeof(SM_CHANNEL_INFO)),
			$"Return cast duration {started.Get<ushort>("castDuration")} and result flags " +
			$"{result.Get<byte>("flags")}; packets: " +
			string.Join(',', session.PacketHistory.Skip(packetStart).Select(packet => packet.PacketType.Name)));
		Assert.Contains(session.PacketHistory.Skip(packetStart), packet =>
			packet.PacketType == typeof(SM_PLAYER_INFO) &&
			packet.Get<int>("objectId") == session.CharacterId);
		session.AcceptTeleportPosition();
		Assert.Equal(NaturalIshalgenRoads.MapId, session.Api.World.MapId);
		policy.AssertClean();
	}

	[SkippableFact]
	public void Q2007DerotToNaltoHasCheckedGroundRoute()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		var geometry = BotNavigationGeometry.ForServerWorld(instanceId: 1, Race.ASMODIANS);
		BotPosition derot = new(948.970f, 1700.725f, 259.625f, 18);
		BotPosition nalto = new(729.102f, 1156.882f, 302.737f, 23);
		IReadOnlyList<BotPosition> route = geometry.FindJourneyPath(220010000, derot, nalto);
		Assert.NotEmpty(route);
		Assert.InRange(Distance(route[^1], nalto), 0, 3);
	}

	[SkippableFact]
	public void Q2007ObservedNaltoApproachHasCheckedGroundRoute()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		var geometry = BotNavigationGeometry.ForServerWorld(instanceId: 1, Race.ASMODIANS);
		BotPosition observedCheckpoint = new(817.381f, 1469.214f, 278.5f, 95);
		BotPosition nalto = new(729.102f, 1156.882f, 302.737f, 23);
		IReadOnlyList<BotPosition> route = geometry.FindJourneyPath(220010000, observedCheckpoint, nalto);
		Assert.NotEmpty(route);
		Assert.InRange(Distance(route[^1], nalto), 0, 3);
	}

	[SkippableFact]
	public void Q2007NaltoBlockedLayoutHasAnObservedGuardOnTheCheckedRoute()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		var geometry = BotNavigationGeometry.ForServerWorld(instanceId: 1, Race.ASMODIANS);
		BotPosition start = new(817.381f, 1469.214f, 278.5f, 95);
		BotPosition nalto = new(729.102f, 1156.882f, 302.737f, 23);
		IReadOnlyList<BotPosition> route = geometry.FindInteractionPath(
			NaturalIshalgenRoads.MapId, start, nalto);
		Assert.NotEmpty(route);
		NaturalNavigationObject[] observed =
		[
			new(1, 210393, new(734.2f, 1511f, 278.5f, 0)),
			new(2, 210393, new(789f, 1527f, 278.5f, 0)),
			new(3, 210609, new(746.5f, 1511.3f, 278.5f, 0)),
			new(4, 210393, new(747.3f, 1494.8f, 278.5f, 0)),
			new(5, 210393, new(780f, 1531.5f, 278.5f, 0)),
		];
		NaturalNavigationObject? guard = NaturalGuardedObjectivePolicy.SelectBlockerOnRoute(
			start, route, observed, id => id == 210609 ? 17 : 9);
		Assert.NotNull(guard);
	}

	[SkippableFact]
	public void Q2007NaltoToRaeHasCheckedInteractionRouteWithoutHostiles()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		var geometry = BotNavigationGeometry.ForServerWorld(instanceId: 1, Race.ASMODIANS);
		BotPosition nalto = new(729.8415f, 1157.7434f, 302.84293f, 76);
		BotPosition rae = new(648.419f, 920.773f, 310.807f, 60);
		IReadOnlyList<BotPosition> route = geometry.FindInteractionPath(
			NaturalIshalgenRoads.MapId, nalto, rae);
		Assert.NotEmpty(route);
		Assert.InRange(Distance(route[^1], rae), 0, 3);
	}

	[SkippableFact]
	public void Q2007RaeNearGuardHasCheckedLineOfSightFlank()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		var geometry = BotNavigationGeometry.ForServerWorld(instanceId: 1, Race.ASMODIANS);
		BotPosition priest = new(729.4907f, 1158.1067f, 302.88165f, 84);
		BotPosition guard = new(728.454f, 1135.643f, 307.546f, 0);
		Assert.False(geometry.HasLineOfSight(NaturalIshalgenRoads.MapId, priest, guard));
		IReadOnlyList<BotPosition> route = geometry.FindRangedApproachPath(
			NaturalIshalgenRoads.MapId, priest, guard,
			[new(new BotPosition(751.596f, 1066.652f, 311f, 0), 8)]);
		Assert.NotEmpty(route);
		Assert.True(geometry.HasLineOfSight(NaturalIshalgenRoads.MapId, route[^1], guard));
	}

	[SkippableFact]
	public void Q2006MauReturnObstacleCannotBePulledFromTheFarSide()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		var geometry = BotNavigationGeometry.ForServerWorld(instanceId: 1, Race.ASMODIANS);
		BotPosition priest = new(682.1575f, 1499.0281f, 296.59772f, 74);
		BotPosition mau = new(690.241f, 1504.157f, 295.461f, 0);
		Assert.False(geometry.HasLineOfSight(NaturalIshalgenRoads.MapId, priest, mau));
		IReadOnlyList<BotPosition> route = geometry.FindRangedApproachPath(
			NaturalIshalgenRoads.MapId, priest, mau, []);
		Assert.Empty(route);
		BotPosition mijou = new(946.253f, 1702.775f, 259.625f, 18);
		Assert.Empty(geometry.FindJourneyPath(NaturalIshalgenRoads.MapId, priest, mijou));
	}

	[SkippableFact]
	public void NaturalBindToUlgornViaPreviouslyVisitedNpcsHasCheckedGround()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		var geometry = BotNavigationGeometry.ForServerWorld(instanceId: 1, Race.ASMODIANS);
		BotPosition[] visited =
		[
			new(571.0388f, 2787.342f, 299.875f, 32), // ordinary Return bind position
			new(526.99f, 2775.67f, 295.751f, 0),       // Vandar
			new(223.975f, 2679.86f, 295.25f, 0),       // Guheitun
			new(220.15f, 2678.81f, 295.25f, 0),        // Vanar
			new(584.949f, 2418.722f, 278.625f, 33),   // Ulgorn
			new(542.419f, 2429.002f, 277.276f, 15),   // Boromer
			new(513.120f, 2432.481f, 277.361f, 96),   // Nobekk
		];
		for (int index = 1; index < visited.Length; index++)
			Assert.True(geometry.FindInteractionPath(NaturalIshalgenRoads.MapId,
				visited[index - 1], visited[index]).Count > 0,
				$"No checked bind-return interaction leg {index}: {visited[index - 1]} to {visited[index]}.");
	}

	[SkippableFact]
	public void TombstoneGuardianHasCheckedOutwardRetreatToRecordedIngress()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		var geometry = BotNavigationGeometry.ForServerWorld(instanceId: 1, Race.ASMODIANS);
		BotPosition lowHpPosition = new(598.83154f, 1880.1667f, 282.5f, 54);
		BotPosition walkedCheckpoint = new(633.0833f, 1854.8467f, 276.47043f, 48);
		BotNavigationHazard[] observedGuardian =
			[new(new BotPosition(600.1f, 1880.8f, 282.5f, 0), 8)];
		IReadOnlyList<BotPosition> route = geometry.FindJourneyPathAvoiding(
			220010000, lowHpPosition, walkedCheckpoint, observedGuardian);
		Assert.NotEmpty(route);
		Assert.True(BotNavigationGeometry.AvoidsHazards(lowHpPosition, route, observedGuardian));
		Assert.InRange(Distance(route[^1], walkedCheckpoint), 0, 3);
	}

	[SkippableFact]
	public void MijouToMauFarmRoadHintHasACollisionCheckedGroundRoute()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		var geometry = BotNavigationGeometry.ForServerWorld(instanceId: 1, Race.ASMODIANS);
		BotPosition mijou = new(946.253f, 1702.775f, 259.625f, 18);
		BotPosition mauSack = new(742.801f, 1515.77f, 292.565f, 0);
		IReadOnlyList<BotPosition> path = geometry.FindRoadPreferredJourneyPath(
			NaturalIshalgenRoads.MapId, mijou, mauSack,
			NaturalIshalgenRoads.MijouToMauFarms, []);
		Assert.NotEmpty(path);
		Assert.Equal(mauSack.X, path[^1].X);
		Assert.Equal(mauSack.Y, path[^1].Y);
		Assert.InRange(MathF.Abs(mauSack.Z - path[^1].Z), 0, 1);
		int nearRoad = path.Count(point => DistanceToRoad(point,
			NaturalIshalgenRoads.MijouToMauFarms) <= 20);
		Assert.True(nearRoad * 2 > path.Count,
			$"Only {nearRoad}/{path.Count} checked ground samples followed the mapped road corridor.");
		foreach (BotPosition point in path)
		{
			Assert.NotNull(geometry.TraceEdge(NaturalIshalgenRoads.MapId, mijou, point));
			mijou = point;
		}
	}

	[SkippableFact]
	public void MauGuardApproachStopsAtAVisibleSpellRangePointOutsideOtherAggro()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		var geometry = BotNavigationGeometry.ForServerWorld(instanceId: 1, Race.ASMODIANS);
		BotPosition start = new(792.64636f, 1527.529f, 283.69864f, 62);
		BotPosition guard = new(746.496f, 1511.299f, 292.974f, 0);
		BotNavigationHazard[] otherHazards =
		[
			new(new(720.7f, 1552.2f, 0, 0), 6),
			new(new(726.8f, 1523.2f, 0, 0), 6),
			new(new(727.1f, 1547.4f, 0, 0), 6),
			new(new(734.0f, 1511.1f, 0, 0), 6),
			new(new(709.4f, 1519.8f, 0, 0), 8),
			new(new(809.6f, 1573.1f, 0, 0), 8),
			new(new(737.3f, 1524.1f, 0, 0), 6),
			new(new(715.6f, 1546.5f, 0, 0), 6),
			new(new(731.5f, 1512.8f, 0, 0), 6),
		];
		IReadOnlyList<BotPosition> path = geometry.FindRangedApproachPath(
			NaturalIshalgenRoads.MapId, start, guard, otherHazards);
		Assert.NotEmpty(path);
		Assert.True(BotNavigationGeometry.AvoidsHazards(start, path, otherHazards));
		Assert.InRange(MathF.Sqrt(MathF.Pow(path[^1].X - guard.X, 2) +
			MathF.Pow(path[^1].Y - guard.Y, 2) + MathF.Pow(path[^1].Z - guard.Z, 2)), 0, 20);
		Assert.True(geometry.HasLineOfSight(NaturalIshalgenRoads.MapId, path[^1], guard));
	}

	[SkippableFact]
	public void MauFarmReturnCanApproachTheFirstObservedCorridorGuard()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		var geometry = BotNavigationGeometry.ForServerWorld(instanceId: 1, Race.ASMODIANS);
		BotPosition start = new(680.505f, 1504.05f, 295.3006f, 90);
		BotPosition guard = new(702.2f, 1519.15f, 294.6f, 0);
		BotNavigationHazard[] otherHazards =
		[
			new(new(690.24f, 1504.16f, 0, 0), 3),
			new(new(713.11f, 1524.38f, 0, 0), 3),
			new(new(746.496f, 1511.299f, 0, 0), 16),
			new(new(665.30f, 1532.75f, 0, 0), 3),
			new(new(648.10f, 1505.92f, 0, 0), 3),
		];
		IReadOnlyList<BotPosition> path = geometry.FindRangedApproachPath(
			NaturalIshalgenRoads.MapId, start, guard, otherHazards);
		Assert.NotEmpty(path);
		Assert.True(BotNavigationGeometry.AvoidsHazards(start, path, otherHazards));
		Assert.True(geometry.HasLineOfSight(NaturalIshalgenRoads.MapId, path[^1], guard));
	}

	[SkippableFact]
	public void MauFarmReturnHasCheckedGroundToMijouWithoutObservedAggro()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		var geometry = BotNavigationGeometry.ForServerWorld(instanceId: 1, Race.ASMODIANS);
		BotPosition lastSack = new(680.505f, 1504.05f, 295.3006f, 90);
		BotPosition mijou = new(946.253f, 1702.775f, 259.625f, 18);
		IReadOnlyList<BotPosition> route = geometry.FindRoadPreferredJourneyPath(
			NaturalIshalgenRoads.MapId, lastSack, mijou,
			NaturalIshalgenRoads.MijouToMauFarms, []);
		Assert.NotEmpty(route);
		Assert.InRange(MathF.Abs(route[^1].X - mijou.X), 0, 1);
		Assert.InRange(MathF.Abs(route[^1].Y - mijou.Y), 0, 1);
	}

	[SkippableFact]
	public void RecordedEasternRoadIngressHasCheckedReverseLegs()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		var geometry = BotNavigationGeometry.ForServerWorld(instanceId: 1, Race.ASMODIANS);
		// Actual client-estimated progress from the earlier Nobekk-to-road-stop
		// traversal. The journey learns these at runtime; this fixture checks that
		// retracing them is ground-checked rather than assuming the direct valley line.
		BotPosition[] walked =
		[
			new(516.12f, 2427.481f, 272.7428f, 0),
			new(500.12f, 2411.481f, 267.13757f, 0),
			new(487.12f, 2392.481f, 259.55048f, 0),
			new(502.12f, 2375.481f, 256.10754f, 0),
			new(518.12f, 2359.481f, 257.43744f, 0),
			new(534.12f, 2343.481f, 255.65024f, 0),
			new(551.12f, 2328.481f, 255.39511f, 0),
			new(568.12f, 2313.481f, 254.63007f, 0),
			new(584.12f, 2297.481f, 254.27768f, 0),
			new(600.12f, 2281.481f, 251.36993f, 0),
			new(624.12f, 2273.481f, 250.18512f, 0),
			new(640.12f, 2257.481f, 251.67993f, 0),
			new(656.12f, 2241.481f, 253.06584f, 0),
			new(675.12f, 2228.481f, 253.11476f, 0),
			new(693.12f, 2214.481f, 251.30017f, 0),
			new(710.12f, 2199.481f, 252.78995f, 0),
			new(726.12f, 2183.481f, 254.99756f, 0),
			new(747.12f, 2172.481f, 255.95671f, 0),
			new(773.12f, 2174.481f, 257.08505f, 0),
			new(796.12f, 2183.481f, 259.31763f, 0),
			new(817.12f, 2194.481f, 262.7448f, 0),
			new(835.12f, 2208.481f, 265.55493f, 0),
			new(856f, 2216f, 266f, 0),
		];
		BotPosition previous = walked[^1];
		for (int index = walked.Length - 2; index >= 0; index--)
		{
			IReadOnlyList<BotPosition> path = geometry.FindLocalPath(
				NaturalIshalgenRoads.MapId, previous, walked[index]);
			Assert.True(path.Count > 0,
				$"No checked reverse eastern-road leg {index}: {previous} to {walked[index]}.");
			previous = walked[index];
		}
	}

	[SkippableFact]
	public void MauFarmEastPocketHasCheckedSpellRangeTerrainWithoutAggro()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		var geometry = BotNavigationGeometry.ForServerWorld(instanceId: 1, Race.ASMODIANS);
		BotPosition pocket = new(783.7217f, 1513.2783f, 283.4826f, 75);
		BotPosition highSitter = new(746.496f, 1511.299f, 292.974f, 0);
		IReadOnlyList<BotPosition> route = geometry.FindRangedApproachPath(
			NaturalIshalgenRoads.MapId, pocket, highSitter, []);
		Assert.NotEmpty(route);
	}

	[SkippableFact]
	public void MauFarmEastPocketIsBlockedByTheObservedAggroPack()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		BotPosition pocket = new(783.7217f, 1513.2783f, 283.4826f, 75);
		BotPosition highSitter = new(746.496f, 1511.299f, 292.974f, 0);
		BotNavigationHazard[] otherHazards =
		[
			new(new(790.4663f, 1528.8455f, 283.67044f, 0), 8),
			new(new(737.255f, 1524.064f, 292.688f, 0), 6),
			new(new(734.1002f, 1510.9092f, 292.7449f, 0), 6),
			new(new(731.499f, 1512.758f, 292.719f, 0), 6),
			new(new(726.794f, 1523.217f, 292.821f, 0), 6),
			new(new(720.4192f, 1547.6571f, 295.87756f, 0), 6),
			new(new(819.9168f, 1577.0363f, 288.02875f, 0), 8),
			new(new(720.6474f, 1552.093f, 295.8757f, 0), 6),
			new(new(709.4f, 1519.799f, 294.25f, 0), 8),
			new(new(715.74164f, 1546.5396f, 295.875f, 0), 6),
			new(new(711.69934f, 1546f, 295.9163f, 0), 9),
			new(new(690.241f, 1504.157f, 295.461f, 0), 8),
			new(new(690.086f, 1521.708f, 295.599f, 0), 8),
			new(new(763.3723f, 1490.9587f, 286.55713f, 0), 8),
			new(new(763.2169f, 1498.8379f, 285.3783f, 0), 8),
		];
		foreach (int instanceId in new[] { 0, 1 })
		{
			var geometry = BotNavigationGeometry.ForServerWorld(instanceId, Race.ASMODIANS);
			IReadOnlyList<BotPosition> route = geometry.FindRangedApproachPath(
				NaturalIshalgenRoads.MapId, pocket, highSitter, otherHazards);
			Assert.Empty(route); // The bot must backtrack or fight; it cannot walk through the pack.
			BotPosition priorVantage = new(788.7217f, 1520.2783f, 283.7152f, 90);
			IReadOnlyList<BotPosition> backtrack = geometry.FindLocalPath(
				NaturalIshalgenRoads.MapId, pocket, priorVantage);
			Assert.NotEmpty(backtrack);
			Assert.True(BotNavigationGeometry.AvoidsHazards(pocket, backtrack, otherHazards));
			BotPosition[] candidateGuards =
			[
				new(763.3723f, 1490.9587f, 286.55713f, 0),
				new(763.2169f, 1498.8379f, 285.3783f, 0),
				new(737.255f, 1524.064f, 292.688f, 0),
				highSitter,
			];
			int[] pocketApproaches = candidateGuards.Select(guard => geometry.FindRangedApproachPath(
				NaturalIshalgenRoads.MapId, pocket, guard,
				otherHazards.Where(hazard => hazard.Position != guard).ToArray()).Count).ToArray();
			int[] approaches = candidateGuards.Select(guard => geometry.FindRangedApproachPath(
				NaturalIshalgenRoads.MapId, priorVantage, guard,
				otherHazards.Where(hazard => hazard.Position != guard).ToArray()).Count).ToArray();
			Assert.True(pocketApproaches.Any(count => count > 0),
				$"No checked ranged guard approach from the pocket: {string.Join(',', pocketApproaches)}.");
			Assert.True(approaches.Any(count => count > 0),
				$"No checked ranged guard approach after retreat: {string.Join(',', approaches)}.");
		}
	}

	private static float DistanceToRoad(BotPosition position, IReadOnlyList<BotRoadPoint> road)
	{
		float nearest = float.PositiveInfinity;
		for (int index = 1; index < road.Count; index++)
		{
			BotRoadPoint a = road[index - 1], b = road[index];
			float dx = b.X - a.X, dy = b.Y - a.Y;
			float lengthSquared = dx * dx + dy * dy;
			float fraction = lengthSquared <= 0 ? 0 : Math.Clamp(
				((position.X - a.X) * dx + (position.Y - a.Y) * dy) / lengthSquared, 0, 1);
			float x = a.X + fraction * dx, y = a.Y + fraction * dy;
			nearest = MathF.Min(nearest, MathF.Sqrt(
				(position.X - x) * (position.X - x) + (position.Y - y) * (position.Y - y)));
		}
		return nearest;
	}
}
