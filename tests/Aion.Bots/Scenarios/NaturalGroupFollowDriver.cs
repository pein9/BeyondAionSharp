using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

/// <summary>F1 is the observed leader. Follow its movement samples, never a separately planned quest route.</summary>
public sealed class NaturalGroupFollowDriver(INaturalJourneySession session, NaturalJourneyRuntime runtime)
{
	private readonly Queue<BotPosition> trail = new();
	private readonly BotNavigationGeometry geometry = runtime.CreateGeometry();
	private int cursor;
	private int? leader;
	private BotPosition? lastSample;
	private DateTimeOffset lastSeen;
	private bool leaderMoving;

	public void Reset() { trail.Clear(); lastSample = null; cursor = session.PacketHistory.Count; }

	public async Task<bool> StepAsync(CancellationToken token)
	{
		await session.SynchronizeAsync(token);
		var world = session.Api.World;
		if (world.GroupLeaderId != leader)
		{
			leader = world.GroupLeaderId;
			trail.Clear(); lastSample = null; leaderMoving = false;
		}
		foreach (var packet in session.PacketHistory.Skip(cursor))
			if (leader is int id && packet.PacketType == typeof(SM_MOVE) && packet.Get<int>("objectId") == id ||
				leader is int member && packet.PacketType == typeof(SM_GROUP_MEMBER_INFO) && packet.Get<int>("objectId") == member &&
				packet.Get<byte>("event") is not (0 or 3 or 7))
			{
				if (packet.PacketType == typeof(SM_MOVE)) leaderMoving = packet.Get<byte>("movementMask") != 0;
				Add(new(packet.Get<float>("x"), packet.Get<float>("y"), packet.Get<float>("z"), 0));
				lastSeen = packet.ReceivedAt ?? session.Api.Timing.Now;
			}
		cursor = session.PacketHistory.Count;
		if (world.IsDead || leader is not int leaderId || leaderId == session.CharacterId ||
			!world.GroupMembers.TryGetValue(leaderId, out var f1) || !f1.Online || f1.MapId != world.MapId)
			return false;
		if (f1.Dead) leaderMoving = false; // The Cleric can approach a fallen F1 to offer resurrection.
		BotPosition currentLeader = world.Objects.GetValueOrDefault(leaderId)?.Position ?? f1.Position;
		if (lastSample == null) { Add(currentLeader); lastSeen = session.Api.Timing.Now; }
		// A stopped player sends no movement heartbeat. Its visible, unchanged position remains usable.
		if ((session.Api.Timing.Now - lastSeen).TotalSeconds > 5 && (leaderMoving || !world.Objects.ContainsKey(leaderId))) return false;
		if (Distance(session.CurrentPosition, currentLeader) <= 3) { trail.Clear(); return false; }
		// Ground snapping can put the follower slightly below an interpolated leader sample on a slope.
		// Requiring the identical Z would leave it standing forever at the same horizontal point.
		while (trail.TryPeek(out var reached) && MathF.Abs(session.CurrentPosition.Z - reached.Z) <= 3 &&
			MathF.Sqrt(MathF.Pow(session.CurrentPosition.X - reached.X, 2) + MathF.Pow(session.CurrentPosition.Y - reached.Y, 2)) <= .8f)
			trail.Dequeue();
		if (!trail.TryPeek(out var next)) return false;
		// A short checked connection to the next leader sample also accommodates a stopped caster.
		var path = geometry.FindLocalPath(world.MapId!.Value, session.CurrentPosition, next);
		if (path.Count == 0)
		{
			session.TraceDiagnostic("group-follow-blocked", new Dictionary<string, object?> { ["leader"] = leaderId, ["next"] = next });
			return false;
		}
		var segment = new List<BotPosition>();
		BotPosition from = session.CurrentPosition;
		float remaining = 3;
		foreach (var point in path)
		{
			float length = Distance(from, point);
			if (length < .01f) continue;
			if (length > remaining)
			{
				float part = remaining / length;
				segment.Add(new(from.X + (point.X - from.X) * part, from.Y + (point.Y - from.Y) * part,
					from.Z + (point.Z - from.Z) * part, point.Heading));
				break;
			}
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
		if (lastSample is { } previous && Distance(previous, point) < .3f) return;
		trail.Enqueue(point); lastSample = point;
		while (trail.Count > 512) trail.Dequeue();
	}
	private static float Distance(BotPosition a, BotPosition b) => NaturalServicePolicy.Distance(a, b);
}
