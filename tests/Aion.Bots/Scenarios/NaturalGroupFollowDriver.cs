using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

/// <summary>F1 is the observed leader. Follow its movement samples, never a separately planned quest route.</summary>
public sealed class NaturalGroupFollowDriver(INaturalJourneySession session, NaturalJourneyRuntime runtime)
{
	private readonly Queue<BotPosition> trail = new();
	private BotNavigationGeometry geometry = runtime.CreateGeometry();
	private (int? Map, int? Instance) place;
	private int cursor;
	private int? leader;
	private BotPosition? lastSample;
	private DateTimeOffset lastSeen;
	private DateTimeOffset blockedUntil;
	// M6: the follower's turns in a row with no checked way to its leader, and where it and its leader stood when the long
	// search for a way found none.
	private int blockedTurns;
	private (BotPosition From, BotPosition To)? noWay;
	private bool leaderMoving;
	private BotPosition? pursued;
	private float pursuedBest, pursuedLast;
	private int pursuedSteps;
	private DateTimeOffset skipLookedAt;
	private readonly Queue<BotPosition> airTrail = new();

	/// <summary>The leader flies: it took off (the FLY emotion) and has not landed.</summary>
	public bool LeaderFlying { get; private set; }
	/// <summary>The leader glides: it opened its wings in a fall and has not closed them.</summary>
	public bool LeaderGliding { get; private set; }
	/// <summary>In the air its samples are no ground to walk, and no way round is looked for until it is down.</summary>
	public bool LeaderAirborne => LeaderFlying || LeaderGliding;
	/// <summary>The leader's client says it is moving: its last movement packet was not a stop.</summary>
	public bool LeaderMoving => leaderMoving;
	/// <summary>
	/// Skip ahead (the operator, 2026-10-13: "yes, build the skip-ahead"). The follower walks its leader's trail sample by
	/// sample, so after a fight it walked every step its leader had taken meanwhile; one was 113 m behind in the camp.
	/// With this on, a checked way straight to where its leader stands replaces the trail when it is shorter than what is
	/// left of the trail. A companion sets it; the group legs keep the trail as it was.
	/// </summary>
	public bool SkipAhead { get; set; }
	/// <summary>M6: the open field's rules (docs/natural-morheim-leveling.md): a leader sample in the air stands on the ground
	/// below it, and a follower drops to a leader just below a ledge. The camp keeps the trail as it was.</summary>
	public bool Field { get; set; }
	/// <summary>Where the leader has been since it took off, oldest first. A follower that flies takes them from here.</summary>
	public Queue<BotPosition> AirTrail => airTrail;

	public void Reset()
	{
		trail.Clear(); lastSample = null; cursor = session.PacketHistory.Count; blockedUntil = default;
		airTrail.Clear(); LeaderFlying = false; LeaderGliding = false;
	}

	/// <summary>The follower came down from a flight of its own: the ground it knew is behind it, and where its leader
	/// stands is the next sample. What it knows of its leader's flight stays.</summary>
	public void Landed() { trail.Clear(); lastSample = null; blockedUntil = default; }

	/// <summary>
	/// Reads what the client has seen of its leader since the last look: where it walked, and whether it is in the air.
	/// <see cref="StepAsync"/> looks by itself; a follower that is flying, and takes no step on the ground, looks here.
	/// </summary>
	public void Observe()
	{
		var world = session.Api.World;
		if ((world.MapId, world.InstanceId) != place)
		{
			// Checked ground belongs to the map and instance the client stands in; a trail of another map leads nowhere.
			place = (world.MapId, world.InstanceId);
			geometry = runtime.CreateGeometry();
			trail.Clear(); lastSample = null; cursor = session.PacketHistory.Count;
			airTrail.Clear(); LeaderFlying = false; LeaderGliding = false;
		}
		if (world.GroupLeaderId != leader)
		{
			leader = world.GroupLeaderId;
			trail.Clear(); lastSample = null; leaderMoving = false;
			airTrail.Clear(); LeaderFlying = false; LeaderGliding = false;
		}
		// The history can have been emptied since the last look (a change of map).
		if (cursor > session.PacketHistory.Count) cursor = session.PacketHistory.Count;
		foreach (var packet in session.PacketHistory.Skip(cursor))
		{
			if (leader is int flier && packet.PacketType == typeof(SM_EMOTION) && packet.Get<int>("senderObjectId") == flier)
			{
				switch ((EmotionType)packet.Get<byte>("emotionType"))
				{
					case EmotionType.FLY: LeaderFlying = true; airTrail.Clear(); break;
					case EmotionType.START_GLIDE: LeaderGliding = true; break;
					// Down again: where it stands now is the next ground sample.
					case EmotionType.LAND or EmotionType.STOP_FLY: LeaderFlying = false; LeaderGliding = false; lastSample = null; break;
					case EmotionType.STOP_GLIDE: LeaderGliding = false; lastSample = null; break;
				}
				continue;
			}
			if (leader is int id && packet.PacketType == typeof(SM_MOVE) && packet.Get<int>("objectId") == id ||
				leader is int member && packet.PacketType == typeof(SM_GROUP_MEMBER_INFO) && packet.Get<int>("objectId") == member &&
				packet.Get<byte>("event") is not (0 or 3 or 7))
			{
				if (packet.PacketType == typeof(SM_MOVE)) leaderMoving = packet.Get<byte>("movementMask") != 0;
				var sample = new BotPosition(packet.Get<float>("x"), packet.Get<float>("y"), packet.Get<float>("z"), 0);
				if (!LeaderAirborne) Add(sample);
				else if (LeaderFlying && packet.PacketType == typeof(SM_MOVE))
				{
					airTrail.Enqueue(sample);
					while (airTrail.Count > 256) airTrail.Dequeue();
				}
				lastSeen = packet.ReceivedAt ?? session.Api.Timing.Now;
			}
		}
		cursor = session.PacketHistory.Count;
	}

	public async Task<bool> StepAsync(CancellationToken token)
	{
		await session.SynchronizeAsync(token);
		var world = session.Api.World;
		Observe();
		if (world.IsDead || leader is not int leaderId || leaderId == session.CharacterId ||
			!world.GroupMembers.TryGetValue(leaderId, out var f1) || !f1.Online || f1.MapId != world.MapId)
			return false;
		if (f1.Dead) leaderMoving = false; // The Cleric can approach a fallen F1 to offer resurrection.
		BotPosition currentLeader = world.Objects.GetValueOrDefault(leaderId)?.Position ?? f1.Position;
		if (lastSample == null && !LeaderAirborne) { Add(currentLeader); lastSeen = session.Api.Timing.Now; }
		// A stopped player sends no movement heartbeat. Its visible, unchanged position remains usable, and so is the trail
		// already recorded toward a leader out of sight. M8: after a long retreat in the camp the Templar stood 80 m off, out
		// of view, and all five followers stood with 48 samples of its way unwalked until its wait for the group ran out.
		if ((session.Api.Timing.Now - lastSeen).TotalSeconds > 5 && (leaderMoving || !world.Objects.ContainsKey(leaderId)) &&
			trail.Count == 0) return false;
		if (Distance(session.CurrentPosition, currentLeader) <= 3) { trail.Clear(); return false; }
		// The follower stood beside its leader with nothing left to walk, and was then moved away while the leader stood
		// still (a fear, a knockback): no sample comes from a leader that does not move. Where it stands is the sample.
		if (trail.Count == 0)
		{
			// In the air its leader stands on nothing: the ground it walked before it took off is all there is to walk.
			if (LeaderAirborne) return false;
			lastSample = null; Add(currentLeader);
		}
		// Ground snapping can put the follower slightly below an interpolated leader sample on a slope.
		// Requiring the identical Z would leave it standing forever at the same horizontal point.
		while (trail.TryPeek(out var reached) && MathF.Abs(session.CurrentPosition.Z - reached.Z) <= 3 &&
			MathF.Sqrt(MathF.Pow(session.CurrentPosition.X - reached.X, 2) + MathF.Pow(session.CurrentPosition.Y - reached.Y, 2)) <= .8f)
			trail.Dequeue();
		if (!trail.TryPeek(out var next) || session.Api.Timing.Now < blockedUntil) return false;
		// It walks and comes no nearer to the sample it is after: the way it finds there leads round in a ring. A Cleric
		// circled a stair of Morheim Ice Fortress for a minute after a sample that lay on the stair's face, three steps to
		// a round, while the Gladiator before it had walked straight on. Such a sample is given up for the one after it.
		if (SkipAhead && !LeaderAirborne && trail.Count >= 6 && (session.Api.Timing.Now - skipLookedAt).TotalSeconds >= 1)
		{
			skipLookedAt = session.Api.Timing.Now;
			BotPosition here = session.CurrentPosition, at = here;
			float left = 0;
			foreach (BotPosition sample in trail) { left += Distance(at, sample); at = sample; }
			float straight = Distance(here, currentLeader);
			if (left > straight + 6)
			{
				IReadOnlyList<BotPosition> direct = [];
				int mapNow = world.MapId!.Value;
				void Look() => direct = geometry.FindLocalPath(mapNow, here, currentLeader);
				if (runtime.SearchElsewhere is { } beside) await beside(Look);
				else Look();
				float length = 0;
				at = here;
				foreach (BotPosition point in direct) { length += Distance(at, point); at = point; }
				// A way that is about as long as the straight line: not one that leads round half the map.
				if (direct.Count > 0 && length <= straight * 1.5f + 5 && length < left - 5)
				{
					session.TraceDiagnostic("group-follow-skip-ahead", new Dictionary<string, object?>
					{ ["leader"] = leaderId, ["trailMetres"] = left, ["directMetres"] = length, ["straightMetres"] = straight, ["samples"] = trail.Count });
					trail.Clear();
					foreach (BotPosition point in direct) trail.Enqueue(point);
					lastSample = direct[^1];
					pursued = null;
					if (!trail.TryPeek(out next)) return false;
				}
			}
		}
		float toNext = Distance(session.CurrentPosition, next);
		// A fight took the follower away from the trail: the distance to the sample leapt, and the count starts again.
		if (pursued is { } same && Distance(same, next) < .01f && toNext <= pursuedLast + 4)
		{
			if (toNext < pursuedBest - .25f) { pursuedBest = toNext; pursuedSteps = 0; }
			else if (++pursuedSteps > 12)
			{
				session.TraceDiagnostic("group-follow-gave-up-sample", new Dictionary<string, object?>
				{ ["leader"] = leaderId, ["sample"] = next, ["nearest"] = pursuedBest, ["now"] = toNext, ["samples"] = trail.Count });
				trail.Dequeue();
				pursued = null;
				return false;
			}
		}
		else { pursued = next; pursuedBest = toNext; pursuedSteps = 0; }
		pursuedLast = toNext;
		// A short checked connection to the next leader sample also accommodates a stopped caster.
		int map = world.MapId!.Value;
		var path = geometry.FindLocalPath(map, session.CurrentPosition, next);
		if (path.Count == 0)
		{
			// A sample can lie on ground this follower cannot step onto from where it stands: the edge beside the stairs F1
			// went down. The samples after it are tried, then a checked route to where F1 is seen now.
			BotPosition refused = next;
			BotPosition standing = session.CurrentPosition;
			BotPosition[] laterSamples = trail.Skip(1).Take(24).ToArray();
			int skipped = 0;
			bool around = false;
			bool airborne = LeaderAirborne;
			void Search()
			{
				foreach (BotPosition later in laterSamples)
				{
					skipped++;
					path = geometry.FindLocalPath(map, standing, later);
					if (path.Count > 0) return;
				}
				skipped = 0;
				// To a leader in the air there is no way on the ground, and looking for one is the longest search there is.
				if (airborne) return;
				// M6: nor again from where it found none to where it found none: a fight left the Templar on a ledge 8 m from his
				// group north of Kimssi, and each search for a way to it held the whole world ten seconds.
				if (noWay is { } none && Distance(none.From, standing) <= 3 && Distance(none.To, currentLeader) <= 3) return;
				path = geometry.FindInteractionPath(map, standing, currentLeader);
				// A way to within reach does not cross a seam of the mesh (the camp's gateway); a way to the exact point does.
				if (path.Count == 0) path = geometry.FindJourneyPath(map, standing, currentLeader);
				around = path.Count > 0;
				noWay = around ? null : (standing, currentLeader);
			}
			if (runtime.SearchElsewhere is { } elsewhere) await elsewhere(Search);
			else Search();
			if (around)
			{
				trail.Clear();
				foreach (BotPosition point in path) trail.Enqueue(point);
				lastSample = path[^1];
				next = path[0];
			}
			else if (path.Count > 0)
			{
				for (int drop = 0; drop < skipped; drop++) trail.Dequeue();
				next = laterSamples[skipped - 1];
			}
			session.TraceDiagnostic(path.Count == 0 ? "group-follow-blocked" : "group-follow-around", new Dictionary<string, object?>
			{ ["leader"] = leaderId, ["refused"] = refused, ["next"] = next, ["samples"] = trail.Count });
			if (path.Count == 0)
			{
				// M6: a leader a few metres down a drop the mesh does not join (a fight carried the Templar down the slope north
				// of Kimssi, and all five followers stood at its top) is reached as a player reaches it: straight down to where
				// it stands, when it is in sight, near and below.
				if (++blockedTurns >= 5 && Field && !airborne && world.Objects.ContainsKey(leaderId) && Distance(session.CurrentPosition, currentLeader) <= 45 &&
					currentLeader.Z < session.CurrentPosition.Z - 3)
				{
					blockedTurns = 0;
					session.TraceDiagnostic("group-follow-drop", new Dictionary<string, object?>
					{ ["leader"] = leaderId, ["from"] = session.CurrentPosition, ["to"] = currentLeader });
					await session.ExecuteMovementAsync(new BotMover(world, session.Api.Timing).CreateFallPlan([currentLeader],
						session.CurrentPosition, world.MovementSpeed!.Value), token);
					trail.Clear(); lastSample = null;
					return true;
				}
				// The searches for a way cost the most time there is: they are not asked again at once.
				blockedUntil = session.Api.Timing.Now + TimeSpan.FromSeconds(blockedTurns > 3 ? 5 : 2);
				return false;
			}
		}
		blockedTurns = 0;
		var segment = new List<BotPosition>();
		BotPosition from = session.CurrentPosition;
		float remaining = 3;
		foreach (var point in path)
		{
			float length = Distance(from, point);
			if (length < .01f) continue;
			// A step ends on a checked sample of the path. A point between two samples can lie on the face of a stair,
			// where no path starts and the follower would stand for good.
			if (length > remaining && segment.Count > 0) break;
			segment.Add(point); remaining -= length; from = point;
			if (remaining <= .01f) break;
		}
		if (segment.Count == 0) return false;
		session.BeginStep("group-follow", "follow-observed-F1-trail");
		session.TraceDiagnostic("group-follow", new Dictionary<string, object?>
		{ ["leader"] = leaderId, ["leaderDistance"] = Distance(session.CurrentPosition, currentLeader), ["next"] = next, ["samples"] = trail.Count });
		await session.ExecuteMovementAsync(new BotMover(world, session.Api.Timing).CreateGroundPlan(segment,
			session.CurrentPosition, world.MovementSpeed!.Value), token);
		return true;
	}

	private void Add(BotPosition point)
	{
		// Group updates can interpolate XY while retaining the previous height on a slope.
		// Place those samples on the same nearby checked ground used by our movement stream.
		if (session.Api.World.MapId is int map && geometry.SnapToGround(map, point) is { } ground && MathF.Abs(ground.Z - point.Z) <= 4)
			point = ground;
		// M6: a leader sample in the air (its step over a dip, east of Kellan's Cabin) stands on the static ground below it.
		else if (Field && session.Api.World.MapId is int under && !LeaderAirborne && geometry.StaticGroundAt(under, point) is null &&
			geometry.StaticGroundBelow(under, point, 14) is { } below)
			point = below;
		if (lastSample is { } previous && Distance(previous, point) < .3f) return;
		trail.Enqueue(point); lastSample = point;
		while (trail.Count > 512) trail.Dequeue();
	}
	private static float Distance(BotPosition a, BotPosition b) => NaturalServicePolicy.Distance(a, b);
}
