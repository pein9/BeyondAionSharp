using System.Text.Json;
using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

/// <summary>The operator's 4.8 Fire Room approach and turning descent, compiled by tools/recording/compile-fire-room-route.py.
/// The recording used a GM account; clearance, ordinary flight zones and a finite FP budget are checked afresh.</summary>
public sealed class NaturalFireRoomTravel
{
	public const float LaunchRadius = 2;
	private const int MaximumLaunchSettles = 8;
	public sealed record Point(int Sequence, long Milliseconds, float X, float Y, float Z, byte Heading, byte Mask, byte GlideFlag)
	{
		public BotPosition At => new(X, Y, Z, Heading);
	}
	public sealed record Route(int MapId, Point[] Points);
	public sealed record GroundPoint(int Sequence, float X, float Y, float Z, byte Heading)
	{
		public BotPosition At => new(X, Y, Z, Heading);
	}
	public sealed record Fight(int NpcId, int SourceSequence, float[] Anchor, float[] PullPosition, GroundPoint[] Approach,
		int DeathSequence, GroundPoint[] Pursuit, string? PatrolRouteId = null)
	{
		public BotPosition Spawn => new(Anchor[0], Anchor[1], Anchor[2], 0);
		public BotPosition Pull => new(PullPosition[0], PullPosition[1], PullPosition[2], 0);
	}
	private sealed record ExitRoute(int PortalNpcId, int SourceUseSequence, GroundPoint[] Points);
	private sealed record UpperLeg(string Mode, GroundPoint[] Points);
	private sealed record Routes(int Schema, Route Entrance, Route ReturnFlight, UpperLeg[] UpperApproach, Route Descent, Fight[] Clearing, Fight Boss, ExitRoute ExitRoute);
	private readonly INaturalJourneySession session;
	private readonly NaturalJourneyRuntime runtime;
	private readonly Func<BotNavigationGeometry> geometry;
	private readonly Routes routes;
	private int? clearingInstance;
	private int clearingPacketStart;
	private readonly Dictionary<int, int> clearingTargets = [];
	private readonly HashSet<int> cleared = [];
	private readonly Dictionary<int, BotPosition[]> clearingPatrols = [];
	public NaturalFireRoomTravel(INaturalJourneySession session, NaturalJourneyRuntime runtime, Func<BotNavigationGeometry> geometry)
	{
		this.session = session;
		this.runtime = runtime;
		this.geometry = geometry;
		routes = JsonSerializer.Deserialize<Routes>(File.ReadAllText(Path.Combine(runtime.RepoRoot,
			"parity-artifacts/e2e/natural-fire-room-route.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
			?? throw new InvalidDataException("Missing recorded Fire Room routes.");
		if (routes.Schema != 6 || routes.Entrance.MapId != 220020000 || routes.ReturnFlight.MapId != 220020000 || routes.Descent.MapId != 320050000 ||
			routes.Entrance.Points.Length < 2 || routes.ReturnFlight.Points.Length < 2 || routes.Descent.Points.Length < 2 ||
			routes.UpperApproach.Length < 2 || routes.UpperApproach.Any(leg => leg.Mode is not ("ground" or "jump" or "fall") ||
				leg.Points.Length < 2 || leg.Points.Any(point => point.Sequence <= 0 ||
					point.Sequence > routes.Descent.Points[0].Sequence || !float.IsFinite(point.X) || !float.IsFinite(point.Y) || point.Z is not (> 605 and < 625))) ||
			NaturalFlightPolicy.Distance(routes.UpperApproach[^1].Points[^1].At, routes.Descent.Points[0].At) > 0.1f ||
			routes.ExitRoute.PortalNpcId != 730046 || routes.ExitRoute.Points.Length < 2 ||
			routes.ExitRoute.Points.Any(point => point.Sequence <= routes.Boss.DeathSequence || point.Sequence >= routes.ExitRoute.SourceUseSequence ||
				!float.IsFinite(point.X) || !float.IsFinite(point.Y) || point.Z is not (> 540 and < 565)) ||
			!routes.Clearing.Select(fight => fight.NpcId).SequenceEqual([212782, 212781, 212781, 212778, 212780]) ||
			routes.Boss.NpcId != 212876 || routes.Clearing.Append(routes.Boss).Any(fight =>
				fight.Anchor.Length != 3 || fight.PullPosition.Length != 3 || !fight.Anchor.Concat(fight.PullPosition).All(float.IsFinite) ||
				fight.Approach.Length == 0 || fight.Approach.Any(point => point.Sequence <= 0 || point.Sequence > fight.SourceSequence ||
					!float.IsFinite(point.X) || !float.IsFinite(point.Y) || point.Z is not (> 540 and < 565)) ||
				NaturalFlightPolicy.Distance(fight.Approach[^1].At, fight.Pull) > 0.1f ||
				fight.DeathSequence <= fight.SourceSequence || fight.Pursuit.Length < 2 ||
				fight.Pursuit.Any(point => point.Sequence < fight.SourceSequence || point.Sequence >= fight.DeathSequence ||
					!float.IsFinite(point.X) || !float.IsFinite(point.Y) || point.Z is not (> 540 and < 565)) ||
				NaturalFlightPolicy.Distance(fight.Pursuit[0].At, fight.Pull) > 0.1f))
			throw new InvalidDataException("Invalid recorded Fire Room routes.");
		foreach (Fight fight in routes.Clearing.Where(fight => fight.PatrolRouteId != null))
		{
			var patrol = runtime.Data.WalkerDataDh.GetWalkerTemplate(fight.PatrolRouteId!)
				?? throw new InvalidDataException($"Missing recorded Fire patrol {fight.PatrolRouteId}.");
			clearingPatrols[fight.SourceSequence] = [.. patrol.GetRouteSteps().Select(step =>
				new BotPosition(step.GetX(), step.GetY(), step.GetZ(), 0))];
		}
	}

	public BotPosition EntranceStart => routes.Entrance.Points[0].At;
	public BotPosition EntranceLanding => routes.Entrance.Points[^1].At;
	public BotPosition DescentStart => routes.Descent.Points[0].At;
	public BotPosition DescentLanding => routes.Descent.Points[^1].At;
	public IReadOnlyList<BotPosition> UpperAirAfter(BotPosition at) => routes.UpperApproach
		.FirstOrDefault(leg => leg.Mode != "ground" && NaturalFlightPolicy.Distance(leg.Points[0].At, at) < 0.1f)
		?.Points.Select(point => point.At).ToArray() ?? [];
	public float DescentMeters => routes.Descent.Points.Zip(routes.Descent.Points.Skip(1),
		(a, b) => NaturalFlightPolicy.Distance(a.At, b.At)).Sum();
	public BotPosition BossPull => routes.Boss.Pull;
	public Fight BossFight => routes.Boss;
	public bool OnLowerFloor => OnLowerFloorAt(session.CurrentPosition);
	private bool OnLowerFloorAt(BotPosition at) => session.Api.World.MapId == routes.Descent.MapId &&
		at.Z is > 540 and < 565 && NaturalFlightPolicy.Distance(at, routes.Boss.Spawn) < 160;
	// An escape from these fights must stay on their floor. The Chanter and Ranger ran onto
	// ground twelve metres below it, where the recorded guard approach could not bring them back.
	public bool IsRetreatGround(BotPosition origin, BotPosition at) => !OnLowerFloorAt(origin) || OnLowerFloorAt(at);
	public Func<BotPosition, BotPosition, float, IReadOnlyList<BotPosition>>? RecoveryForObservedTarget(int target)
	{
		if (!OnLowerFloor || clearingInstance != session.Api.World.InstanceId) return null;
		if (!session.Api.World.Objects.TryGetValue(target, out BotKnownObject? npc)) return null;
		Fight? fight = routes.Clearing.FirstOrDefault(candidate => Matches(candidate, npc));
		if (fight != null) clearingTargets[fight.SourceSequence] = target;
		if (fight == null && npc.TemplateId == routes.Boss.NpcId)
			fight = routes.Boss;
		return fight == null ? null : (start, at, reach) => FindFightRecoveryRoute(fight, start, at, reach);
	}

	private bool Matches(Fight fight, BotKnownObject npc)
	{
		if (npc.Kind != BotKnownObjectKind.Npc || npc.TemplateId != fight.NpcId) return false;
		if (clearingTargets.GetValueOrDefault(fight.SourceSequence) == npc.ObjectId) return true;
		bool AtSource(BotPosition at) => clearingPatrols.TryGetValue(fight.SourceSequence, out BotPosition[]? path)
			? path.Zip(path.Skip(1)).Any(pair => DistanceToSegment(at, pair.First, pair.Second) < 1)
			: NaturalFlightPolicy.Distance(at, fight.Spawn) < 1;
		// An NPC may be announced between route legs, after an earlier chase. Its observed WALK
		// legs identify the native patrol too; RUN legs do not establish a patrol identity.
		if (fight.PatrolRouteId != null && (npc.PatrolPath?.Any(AtSource) == true ||
			session.PacketHistory.Skip(clearingPacketStart).Any(packet => packet.PacketType == typeof(SM_MOVE) &&
				packet.Get<int>("objectId") == npc.ObjectId && packet.Get<byte>("movementMask") is 0xEA or 0xE8 or 0xEE or 0xEC &&
				AtSource(new BotPosition(packet.Get<float>("x"), packet.Get<float>("y"), packet.Get<float>("z"), 0))))) return true;
		return session.PacketHistory.Any(packet => packet.PacketType == typeof(SM_NPC_INFO) &&
			packet.Get<int>("objectId") == npc.ObjectId && packet.Get<int>("npcId") == fight.NpcId &&
			AtSource(new BotPosition(packet.Get<float>("x"), packet.Get<float>("y"), packet.Get<float>("z"), 0)));
	}

	private void ResetClearing()
	{
		if (clearingInstance == session.Api.World.InstanceId)
		{
			// A bind revival can return to this same private copy. Rewalk the corridor, retaining the
			// explicit kill evidence; a later live spawn announcement still invalidates an old death.
			cleared.Clear();
			return;
		}
		clearingInstance = session.Api.World.InstanceId;
		clearingPacketStart = session.PacketHistory.Count;
		clearingTargets.Clear();
		cleared.Clear();
	}

	private Task<bool> WalkApproachAsync(Fight fight, Func<BotPosition, Task<bool>> walk, Func<bool> survived, CancellationToken token) =>
		// An alive retreat can return behind guards already cleared in this copy. Keep their
		// connecting walks available when joining this fight; its own approach may start beyond the deck.
		WalkRecordedGroundAsync([.. routes.Clearing.TakeWhile(prior => prior.SourceSequence != fight.SourceSequence)
			.SelectMany(prior => prior.Approach), .. fight.Approach], fight.NpcId, walk, survived, token);

	public async Task<bool> ApproachDescentAsync(Func<BotPosition, Task<bool>> walk, Func<bool> survived, CancellationToken token)
	{
		if (!survived()) return false;
		if (NaturalFlightPolicy.Distance(session.CurrentPosition, DescentStart) <= LaunchRadius) return true;
		// A fight or loot sweep may have moved along the demonstrated approach. Join
		// its nearest supported segment; never join the middle of a jump or fall.
		int first = Enumerable.Range(0, routes.UpperApproach.Length).Where(index => routes.UpperApproach[index].Mode == "ground")
			.MinBy(index => routes.UpperApproach[index].Points.Min(point => NaturalFlightPolicy.Distance(session.CurrentPosition, point.At)));
		foreach (UpperLeg leg in routes.UpperApproach.Skip(first))
		{
			if (leg.Mode == "ground")
			{
				if (!await WalkRecordedGroundAsync(leg.Points, -1, walk, survived, token)) return false;
				continue;
			}
			if (!await walk(leg.Points[0].At) || !survived()) return false;
			BotNavigationGeometry geo = geometry();
			foreach (GroundPoint point in new[] { leg.Points[0], leg.Points[^1] })
				if (geo.StaticGroundAt(routes.Descent.MapId, point.At) is not { } ground || MathF.Abs(ground.Z - point.Z) > 0.5f)
					throw new InvalidDataException($"Recorded Fire {leg.Mode} endpoint is unsupported at source sequence {point.Sequence}.");
			BotPosition[] points = [session.CurrentPosition, .. leg.Points.Select(point => point.At)];
			if (points.Zip(points.Skip(1)).Any(pair => !NaturalFlightProtocol.IsClear(geo, routes.Descent.MapId, pair.First, pair.Second)))
				throw new InvalidDataException($"Recorded Fire {leg.Mode} is blocked at source sequence {leg.Points[0].Sequence}.");
			float speed = session.Api.World.MovementSpeed ?? 0;
			if (!float.IsFinite(speed) || speed <= 0) return false;
			session.TraceDiagnostic("fire-recorded-upper-air-step", new Dictionary<string, object?>
			{ ["mode"] = leg.Mode, ["sourceStart"] = leg.Points[0].Sequence, ["sourceEnd"] = leg.Points[^1].Sequence });
			var mover = new BotMover(session.Api.World, session.Api.Timing);
			BotMovementPlan plan = leg.Mode == "jump"
				? mover.CreateJumpPlan(points[1..], session.CurrentPosition, speed)
				: mover.CreateFallPlan(points[1..], session.CurrentPosition, speed);
			await session.ExecuteMovementAsync(plan, token);
			await session.SynchronizeAsync(token);
			if (!survived() || NaturalFlightPolicy.Distance(session.CurrentPosition, points[^1]) > 2) return false;
		}
		return true;
	}

	public async Task<bool> ApproachExitAsync(Func<BotPosition, Task<bool>> walk, Func<Task<bool>> settle,
		Func<bool> survived, CancellationToken token) => OnLowerFloor && survived() && await settle() && survived() &&
		await WalkRecordedGroundAsync(routes.ExitRoute.Points, routes.ExitRoute.PortalNpcId, walk, survived, token);

	private async Task<bool> WalkRecordedGroundAsync(GroundPoint[] points, int template, Func<BotPosition, Task<bool>> walk,
		Func<bool> survived, CancellationToken token)
	{
		BotNavigationGeometry geo = geometry();
		foreach (GroundPoint point in points)
			if (geo.StaticGroundAt(routes.Descent.MapId, point.At) is not { } ground || MathF.Abs(ground.Z - point.Z) > 0.5f)
				throw new InvalidDataException($"Recorded Fire ground is unsupported at source sequence {point.Sequence}.");
		foreach (var pair in points.Zip(points.Skip(1)))
			if (geo.TraceEdge(routes.Descent.MapId, pair.First.At, pair.Second.At) == null)
				throw new InvalidDataException($"Recorded Fire walk is blocked before source sequence {pair.Second.Sequence}.");
		int first = Enumerable.Range(0, points.Length).MinBy(index => NaturalFlightPolicy.Distance(session.CurrentPosition, points[index].At));
		for (int index = first; index < points.Length; index++)
		{
			token.ThrowIfCancellationRequested();
			GroundPoint point = points[index];
			if (!survived()) return false;
			session.TraceDiagnostic("fire-recorded-ground-waypoint", new Dictionary<string, object?>
			{ ["sourceSequence"] = point.Sequence, ["position"] = session.CurrentPosition, ["goal"] = point.At, ["template"] = template });
			if (!await walk(point.At) || !survived()) return false;
		}
		return true;
	}

	/// <summary>The recorded guard order, using current observed identities and ordinary combat. Later attempts recheck
	/// the two spirits beside Bolcan for respawns; guards already cleared behind the player are not farmed again.</summary>
	public async Task<bool> ClearBeforeBossAsync(Func<BotPosition, Task<bool>> walk, Func<Task<bool>> settle,
		Func<int, Fight, Task<bool>> kill, Func<bool> survived, CancellationToken token)
	{
		if (!OnLowerFloor || !survived()) return false;
		if (clearingInstance != session.Api.World.InstanceId) ResetClearing();
		BotKnownObject? Find(Fight fight) => session.Api.World.Objects.Values
			.Where(npc => npc.Kind == BotKnownObjectKind.Npc && npc.TemplateId == fight.NpcId && npc.Position.Z is > 540 and < 565 && Matches(fight, npc))
			.OrderBy(npc => npc.IsCorpse).ThenBy(npc => NaturalFlightPolicy.Distance(npc.Position, fight.Spawn)).FirstOrDefault();
		bool SawDeath(int target, int kind)
		{
			bool dead = false, matches = true;
			foreach (var packet in session.PacketHistory.Skip(clearingPacketStart))
			{
				if (packet.PacketType == typeof(SM_NPC_INFO) && packet.Get<int>("objectId") == target)
				{
					matches = packet.Get<int>("npcId") == kind;
					if (!matches || (packet.Get<ushort>("state") & 15) is not (7 or 8)) dead = false; // Explicit live respawn invalidates an old kill.
				}
				if (matches && BotAttackStatus.CarriesHp(packet) && packet.Get<int>("objectId") == target && packet.Get<byte>("hpOrMp") == 0) dead = true;
			}
			return dead;
		}
		// Bind all visible guards before defense or looting can remove one from view.
		foreach (Fight fight in routes.Clearing)
			if (Find(fight) is { } seen) clearingTargets[fight.SourceSequence] = seen.ObjectId;
		foreach (Fight fight in routes.Clearing)
		{
			if (cleared.Contains(fight.SourceSequence) && fight.NpcId is not (212778 or 212780)) continue;
			await session.SynchronizeAsync(token);
			if (!survived() || !OnLowerFloor) return false;
			BotKnownObject? target = Find(fight);
			if (target != null) clearingTargets[fight.SourceSequence] = target.ObjectId;
			bool Dead() => target?.IsCorpse == true || clearingTargets.TryGetValue(fight.SourceSequence, out int prior) && SawDeath(prior, fight.NpcId);
			if (cleared.Contains(fight.SourceSequence) && Dead()) continue;
			// Even an already dead guard's connecting walk is needed after a new descent. Skipping it
			// would send the next guard's approach straight across the raised platform again.
			if (!await WalkApproachAsync(fight, walk, survived, token) || !await settle() || !survived()) return false;
			target = Find(fight);
			if (target != null) clearingTargets[fight.SourceSequence] = target.ObjectId;
			if (!Dead())
			{
				if (fight.PatrolRouteId != null && target is { IsCorpse: false } && !Dead())
				{
					var template = runtime.Data.NpcDataDh.GetNpcTemplate(fight.NpcId)
						?? throw new InvalidDataException($"Missing recorded Fire guard {fight.NpcId}.");
					float contactRange = template.GetAttackRange() + template.GetBoundRadius().GetMaxOfFrontAndSide() + 1;
					long started = runtime.NowMillis;
					// The recording's Guardian walks west and comes to the landing before the first melee
					// cast. Patrol phase differs in a new world; do not try to cross its guarded platform.
					while (runtime.NowMillis - started < 120_000 && target is { IsCorpse: false } && !Dead() &&
						NaturalFlightPolicy.Distance(session.CurrentPosition, target.Position) > contactRange)
					{
						await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
						await session.SynchronizeAsync(token);
						if (!survived() || !await settle() || !survived()) return false;
						if (NaturalFlightPolicy.Distance(session.CurrentPosition, fight.Pull) > LaunchRadius &&
							(!await walk(fight.Pull) || !survived())) return false;
						target = Find(fight);
						if (target != null) clearingTargets[fight.SourceSequence] = target.ObjectId;
					}
					session.TraceDiagnostic("fire-recorded-patrol-wait", new Dictionary<string, object?>
					{ ["template"] = fight.NpcId, ["sourceSequence"] = fight.SourceSequence, ["elapsedMillis"] = runtime.NowMillis - started,
						["position"] = session.CurrentPosition, ["targetPosition"] = target?.Position, ["contactRange"] = contactRange });
					if (target is { IsCorpse: false } && !Dead() &&
						NaturalFlightPolicy.Distance(session.CurrentPosition, target.Position) > contactRange) return false;
				}
				if (target is { IsCorpse: false } && !Dead())
				{
					session.TraceDiagnostic("fire-recorded-guard-pull", new Dictionary<string, object?>
					{ ["template"] = fight.NpcId, ["target"] = target.ObjectId, ["sourceSequence"] = fight.SourceSequence,
						["position"] = session.CurrentPosition, ["recordedPull"] = fight.Pull, ["instance"] = clearingInstance });
					if (!await kill(target.ObjectId, fight) || !survived()) return false;
				}
				else if (!Dead())
				{
					session.TraceDiagnostic("fire-recorded-guard-unobserved", new Dictionary<string, object?>
					{ ["template"] = fight.NpcId, ["sourceSequence"] = fight.SourceSequence, ["position"] = session.CurrentPosition });
					return false; // Absence alone is not proof that the corridor is clear.
				}
			}
			cleared.Add(fight.SourceSequence);
			if (!await settle() || !survived()) return false; // Includes the ordinary mandatory corpse-loot sweep.
		}
		// Loot and rest can move the player. Finish them before returning to the demonstrated pull origin.
		if (!await settle() || !survived() || !await WalkApproachAsync(routes.Boss, walk, survived, token)) return false;
		foreach (Fight fight in routes.Clearing.Where(fight => fight.NpcId is 212778 or 212780))
			if (Find(fight) is { IsCorpse: false } live && !SawDeath(live.ObjectId, fight.NpcId))
			{
				session.TraceDiagnostic("fire-recorded-guard-respawned-before-boss", new Dictionary<string, object?>
				{ ["template"] = fight.NpcId, ["target"] = live.ObjectId, ["position"] = session.CurrentPosition });
				return false;
			}
		return true;
	}

	/// <summary>A short pursuit on this fight's demonstrated foot corridor. It never extrapolates beyond the
	/// recording or routes around another guard; normal combat observes health and attackers after each move.</summary>
	public IReadOnlyList<BotPosition> FindFightRecoveryRoute(Fight fight, BotPosition start, BotPosition target, float reach)
	{
		if (!OnLowerFloor) return [];
		BotPosition[] points = [.. fight.Pursuit.Select(point => point.At)];
		var lengths = new float[points.Length];
		for (int i = 1; i < points.Length; i++) lengths[i] = lengths[i - 1] + NaturalFlightPolicy.Distance(points[i - 1], points[i]);
		(float Along, float Distance) Project(BotPosition at)
		{
			(float Along, float Distance) best = (0, float.PositiveInfinity);
			for (int i = 1; i < points.Length; i++)
			{
				float span = lengths[i] - lengths[i - 1];
				if (span <= 0.001f) continue;
				BotPosition a = points[i - 1], b = points[i];
				float t = Math.Clamp(((at.X - a.X) * (b.X - a.X) + (at.Y - a.Y) * (b.Y - a.Y) +
					(at.Z - a.Z) * (b.Z - a.Z)) / (span * span), 0, 1);
				float distance = NaturalFlightPolicy.Distance(at, Between(a, b, t));
				if (distance < best.Distance) best = (lengths[i - 1] + t * span, distance);
			}
			return best;
		}
		BotPosition At(float along)
		{
			for (int i = 1; i < points.Length; i++)
				if (lengths[i] >= along && lengths[i] > lengths[i - 1])
					return Between(points[i - 1], points[i], (along - lengths[i - 1]) / (lengths[i] - lengths[i - 1]));
			return points[^1];
		}
		var from = Project(start);
		var to = Project(target);
		// NPC body positions can lie beside the player's path. A distant chase or a retreat away
		// from this demonstrated corridor must still use the ordinary planner.
		if (from.Distance > 2 || to.Distance > 6 || MathF.Abs(to.Along - from.Along) < 0.25f) return [];
		float direction = MathF.Sign(to.Along - from.Along);
		float end = from.Along + direction * MathF.Min(12 - from.Distance, MathF.Abs(to.Along - from.Along));
		BotPosition goal = At(end);
		if (NaturalFlightPolicy.Distance(goal, target) >= NaturalFlightPolicy.Distance(start, target) - 0.25f ||
			NaturalFlightPolicy.Distance(start, target) <= reach) return [];
		var waypoints = new List<BotPosition> { At(from.Along) };
		IEnumerable<int> indices = direction > 0 ? Enumerable.Range(0, points.Length) : Enumerable.Range(0, points.Length).Reverse();
		foreach (int i in indices)
			if (direction * (lengths[i] - from.Along) > 0 && direction * (end - lengths[i]) > 0) waypoints.Add(points[i]);
		waypoints.Add(goal);
		var route = new List<BotPosition>();
		BotPosition previous = start;
		BotNavigationGeometry geo = geometry();
		foreach (BotPosition waypoint in waypoints)
		{
			if (NaturalFlightPolicy.Distance(previous, waypoint) < 0.05f) continue;
			IReadOnlyList<BotPosition>? edge = geo.TraceEdge(routes.Descent.MapId, previous, waypoint);
			if (edge == null || edge.Any(point => MathF.Abs(point.Z - waypoint.Z) > 0.5f ||
				geo.StaticGroundAt(routes.Descent.MapId, point) is not { } ground || MathF.Abs(ground.Z - point.Z) > 0.5f)) return [];
			route.AddRange(edge.Where(point => NaturalFlightPolicy.Distance(previous, point) > 0.05f));
			previous = waypoint;
		}
		if (route.Count > 0)
			session.TraceDiagnostic("fire-recorded-fight-pursuit", new Dictionary<string, object?>
			{ ["sourceSequence"] = fight.SourceSequence, ["deathSequence"] = fight.DeathSequence, ["template"] = fight.NpcId,
				["from"] = start, ["targetPosition"] = target, ["goal"] = goal, ["routePoints"] = route.Count });
		return route;
	}

	private static float DistanceToSegment(BotPosition at, BotPosition start, BotPosition end)
	{
		float dx = end.X - start.X, dy = end.Y - start.Y, dz = end.Z - start.Z;
		float square = dx * dx + dy * dy + dz * dz;
		float t = square > 0 ? Math.Clamp(((at.X - start.X) * dx + (at.Y - start.Y) * dy + (at.Z - start.Z) * dz) / square, 0, 1) : 0;
		return NaturalFlightPolicy.Distance(at, Between(start, end, t));
	}

	/// <summary>Only the upper Fire floor to the lower Fire floor; never intercept another room or an upward trip.</summary>
	public bool HandlesDescent(int map, BotPosition goal) => map == routes.Descent.MapId &&
		session.CurrentPosition.X < 140 && session.CurrentPosition.Y < 130 && session.CurrentPosition.Z > 605 &&
		goal.X > 170 && goal.X < 300 && goal.Y > 150 && goal.Y < 310 && goal.Z is > 540 and < 565;

	public Task<bool> EnterAsync(Func<BotPosition, Task<bool>> walk, Func<Task<bool>> settle, CancellationToken token) =>
		FlyRecordedAsync(routes.Entrance, walk, settle, "fire-entrance-flight", token);

	public Task<bool> ReturnToBulaganAsync(Func<BotPosition, Task<bool>> walk, Func<Task<bool>> settle, CancellationToken token) =>
		FlyRecordedAsync(routes.ReturnFlight, walk, settle, "fire-return-flight", token);

	private async Task<bool> FlyRecordedAsync(Route route, Func<BotPosition, Task<bool>> walk, Func<Task<bool>> settle,
		string diagnostic, CancellationToken token)
	{
		if (session.Api.World.MapId != route.MapId || session.Api.World.IsDead) return false;
		// The bot may already be on the Fire portal's side after an earlier attempt.
		if (NaturalFlightPolicy.Distance(session.CurrentPosition, route.Points[^1].At) < 3) return true;
		if (!await PrepareAtStartAsync(route, walk, settle)) return false;
		BotNavigationGeometry geo = geometry();
		IReadOnlyList<NaturalFlyZone> zones = NaturalFlyZone.ForMap(runtime.RepoRoot, runtime.Data.WorldMaps2.GetTemplate(route.MapId), route.MapId);
		CheckClear(route, geo);
		// Check every sampled point of every segment, not just its endpoints. Java ZoneInstance uses the feet Z.
		foreach (var pair in route.Points.Zip(route.Points.Skip(1)))
		{
			int count = Math.Max(1, (int)MathF.Ceiling(NaturalFlightPolicy.Distance(pair.First.At, pair.Second.At)));
			for (int i = 0; i <= count; i++)
			{
				BotPosition at = Between(pair.First.At, pair.Second.At, (float)i / count);
				if (!zones.Any(zone => !zone.Forbids && zone.Contains(at.X, at.Y, at.Z)) ||
					zones.Any(zone => zone.Forbids && zone.Contains(at.X, at.Y, at.Z)))
					throw new InvalidDataException($"Recorded Fire entrance leaves the ordinary FLY zone at {at}.");
			}
		}
		// Budget conservatively at ground speed before takeoff; replan at the announced flight speed afterward.
		float speed = session.Api.World.MovementSpeed ?? 0;
		if (speed <= 0 || !await ReadyAsync(route, Needed(route, speed, false), () => PrepareAtStartAsync(route, walk, settle), token)) return false;
		speed = await NaturalFlightProtocol.TakeOffAsync(session, token);
		if (session.Api.World.CurrentFlightTime < Needed(route, speed, false))
		{
			await NaturalFlightProtocol.LandAsync(session, token);
			return false;
		}
		await session.ExecuteMovementAsync(Plan(route, speed, false), token);
		await NaturalFlightProtocol.LandAsync(session, token);
		await session.SynchronizeAsync(token);
		return Finish(route, diagnostic);
	}

	public async Task<bool> DescendAsync(Func<BotPosition, Task<bool>> walk, Func<Task<bool>> settle, CancellationToken token)
	{
		Route route = routes.Descent;
		// A retained character can log back into this room wounded. Recover with
		// ordinary consumables/rest before walking through its upper guards.
		int beforeHp = session.Api.World.CurrentHp;
		if (session.Api.World.MapId != route.MapId || session.Api.World.IsDead || !await settle()) return false;
		session.TraceDiagnostic("fire-upper-prepared", new Dictionary<string, object?>
		{ ["hpBefore"] = beforeHp, ["hpAfter"] = session.Api.World.CurrentHp, ["maxHp"] = session.Api.World.MaxHp,
			["mp"] = session.Api.World.CurrentMp, ["position"] = session.CurrentPosition });
		if (!await PrepareAtStartAsync(route, walk, settle)) return false;
		CheckClear(route, geometry());
		float speed = session.Api.World.MovementSpeed ?? 0;
		if (speed <= 0 || !await ReadyAsync(route, Needed(route, speed, true), () => PrepareAtStartAsync(route, walk, settle), token)) return false;
		speed = session.Api.World.MovementSpeed ?? 0;
		if (speed <= 0 || session.Api.World.CurrentFlightTime < Needed(route, speed, true)) return false;
		int firstGlide = Array.FindIndex(route.Points, point => (point.Mask & MovementMask.GLIDE) != 0);
		if (firstGlide < 1) throw new InvalidDataException("Recorded Fire descent has no jump before gliding.");
		if (!NaturalFlightProtocol.IsClear(geometry(), route.MapId, session.CurrentPosition, route.Points[firstGlide].At))
			throw new InvalidDataException("Recorded Fire jump is blocked.");
		// Walk to the recorded brink, jump off, then keep GLIDE set through all the turns and upwind samples.
		var mover = new BotMover(session.Api.World, session.Api.Timing);
		await session.ExecuteMovementAsync(mover.CreateJumpPlan([route.Points[firstGlide].At], session.CurrentPosition, speed), token);
		await session.ExecuteMovementAsync(Plan(route with { Points = route.Points[firstGlide..] }, speed, true), token);
		await session.SynchronizeAsync(token);
		bool landed = Finish(route, "fire-recorded-descent");
		if (landed) ResetClearing();
		return landed;
	}

	private bool AtStart(Route route) => !session.Api.World.IsDead && session.Api.World.CurrentHp > 0 && session.Api.World.MapId == route.MapId &&
		NaturalFlightPolicy.Distance(session.CurrentPosition, route.Points[0].At) <= LaunchRadius;

	private async Task<bool> PrepareAtStartAsync(Route route, Func<BotPosition, Task<bool>> walk, Func<Task<bool>> settle)
	{
		for (int attempt = 0; attempt < MaximumLaunchSettles; attempt++)
		{
			if (session.Api.World.MapId != route.MapId || session.Api.World.IsDead || !await walk(route.Points[0].At)) return false;
			if (!await settle()) return false;
			if (AtStart(route)) return true;
			// Defense, rest and the required corpse-loot sweep can move away from the brink. Finish that work,
			// then walk back; never glide from the last corpse's position or skip its loot to keep the launch fixed.
			if (session.Api.World.MapId != route.MapId || session.Api.World.IsDead || session.Api.World.CurrentHp <= 0) return false;
			session.TraceDiagnostic("fire-launch-moved-during-settle", new Dictionary<string, object?>
			{
				["map"] = route.MapId, ["position"] = session.CurrentPosition, ["launch"] = route.Points[0].At, ["attempt"] = attempt,
			});
		}
		return false;
	}

	private void CheckClear(Route route, BotNavigationGeometry geo)
	{
		if (geo.StaticGroundAt(route.MapId, route.Points[^1].At) is not { } ground ||
			MathF.Abs(ground.Z - route.Points[^1].Z) > 1)
			throw new InvalidDataException("Recorded Fire route no longer ends on static ground.");
		foreach (var pair in route.Points.Zip(route.Points.Skip(1)))
			if (!NaturalFlightProtocol.IsClear(geo, route.MapId, pair.First.At, pair.Second.At))
				throw new InvalidDataException($"Recorded Fire route is blocked before source sequence {pair.Second.Sequence}.");
		if (!NaturalFlightProtocol.IsClear(geo, route.MapId, session.CurrentPosition, route.Points[0].At))
			throw new InvalidDataException("Recorded Fire route start is blocked.");
	}

	private static int Needed(Route route, float speed, bool glide) =>
		checked((int)Math.Ceiling(Plan(route, speed, glide).Duration.TotalSeconds) * (glide ? 2 : 1) +
			(glide ? 5 : NaturalFlightPolicy.LandingReserveFp)); // The same five-FP reserve as NaturalFloorGlide.

	private async Task<bool> ReadyAsync(Route route, int need, Func<Task<bool>> prepare, CancellationToken token)
	{
		if (need > session.Api.World.MaxFlightTime)
		{
			session.TraceDiagnostic("fire-route-fp-refused", new Dictionary<string, object?> { ["need"] = need, ["max"] = session.Api.World.MaxFlightTime });
			return false;
		}
		// Stay on the launch floor and defend between recovery ticks. Never wait while already airborne.
		for (int tick = 0; tick < 100 && session.Api.World.CurrentFlightTime < need; tick++)
		{
			await session.AdvanceAsync(TimeSpan.FromSeconds(3), token);
			await session.SynchronizeAsync(token);
			if (!await prepare()) return false;
		}
		// Java rejects a second glide inside the ten-second flight reuse window.
		await session.AdvanceAsync(TimeSpan.FromSeconds(10), token);
		return await prepare() && AtStart(route) && session.Api.World.CurrentFlightTime >= need;
	}

	private bool Finish(Route route, string diagnostic)
	{
		bool landed = !session.Api.World.IsDead && session.Api.World.MapId == route.MapId &&
			NaturalFlightPolicy.Distance(session.CurrentPosition, route.Points[^1].At) < 2;
		session.TraceDiagnostic(diagnostic, new Dictionary<string, object?>
		{
			["landed"] = landed, ["position"] = session.CurrentPosition, ["fp"] = session.Api.World.CurrentFlightTime,
			["hp"] = session.Api.World.CurrentHp, ["sourceStart"] = route.Points[0].Sequence, ["sourceEnd"] = route.Points[^1].Sequence,
		});
		return landed;
	}

	/// <summary>Never faster than either the observed client timing or the bot's own speed. Absolute glide samples preserve
	/// the recording's upwind flags; the short final drop uses FALL, so Java still applies ordinary fall damage.</summary>
	private static BotMovementPlan Plan(Route route, float speed, bool glide)
	{
		if (!float.IsFinite(speed) || speed <= 0) throw new ArgumentOutOfRangeException(nameof(speed));
		var frames = new List<BotMovementFrame>();
		TimeSpan total = TimeSpan.Zero;
		float meters = 0;
		foreach (var pair in route.Points.Zip(route.Points.Skip(1)))
		{
			BotPosition from = pair.First.At, to = pair.Second.At;
			float distance = NaturalFlightPolicy.Distance(from, to);
			TimeSpan duration = TimeSpan.FromSeconds(Math.Max(distance / speed, (pair.Second.Milliseconds - pair.First.Milliseconds) / 1000d));
			bool gliding = glide && (pair.First.Mask & MovementMask.GLIDE) != 0;
			byte mask = (byte)(MovementMask.POSITION | MovementMask.ABSOLUTE | (gliding ? MovementMask.GLIDE : glide && to.Z < from.Z - .05f ? MovementMask.FALL : 0));
			frames.Add(new(TimeSpan.Zero, GameClientPackets.Move(new MovementPacketData(from.X, from.Y, from.Z, from.Heading,
				(byte)(mask | MovementMask.MANUAL), X2: to.X, Y2: to.Y, Z2: to.Z, GlideFlag: pair.First.GlideFlag)), from));
			int pieces = Math.Max(1, (int)Math.Ceiling(duration.TotalMilliseconds / 500));
			for (int i = 1; i <= pieces; i++)
			{
				BotPosition at = Between(from, to, (float)i / pieces);
				frames.Add(new(duration / pieces, GameClientPackets.Move(new MovementPacketData(at.X, at.Y, at.Z, at.Heading, mask,
					GlideFlag: pair.First.GlideFlag)), at));
			}
			total += duration;
			meters += distance;
		}
		BotPosition end = route.Points[^1].At;
		frames.Add(new(TimeSpan.Zero, GameClientPackets.Move(new MovementPacketData(end.X, end.Y, end.Z, end.Heading, MovementMask.IMMEDIATE)), end));
		return new(frames, total, meters);
	}

	private static BotPosition Between(BotPosition a, BotPosition b, float t) =>
		new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, b.Heading);
}
