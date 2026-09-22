using Aion.Bots.World;

namespace Aion.Bots.Navigation;

public sealed record NaturalNavigationObject(int ObjectId, int TemplateId, BotPosition Position);
public sealed record NaturalNavigationObservation(int? MapId, BotPosition? Position, bool IsDead,
	IReadOnlyList<NaturalNavigationObject> Npcs);
public sealed record NaturalNavigationEvent(int Sequence, string Action, string Outcome, string Reason,
	int MapId, int TargetTemplateId, BotPosition? Position, BotPosition Destination, int? TargetObjectId,
	int RouteSearches, int Segments, BotPosition[]? PlannedRoute);
public sealed record NaturalNavigationResult(bool Arrived, string Reason, int? TargetObjectId,
	int RouteSearches, int Segments);

public interface INaturalNavigationDriver
{
	NaturalNavigationObservation Observe();
	Task<IReadOnlyList<BotPosition>> FindRouteAsync(BotPosition start, BotPosition destination, CancellationToken token);
	Task MoveAsync(IReadOnlyList<BotPosition> segment, CancellationToken token);
	Task SynchronizeAsync(CancellationToken token);
	void Record(NaturalNavigationEvent navigationEvent);
}

/// <summary>Client-observed, bounded ground approach. Its position is a paced client estimate, not a server echo.</summary>
public static class NaturalIshalgenNavigator
{
	private const float ArrivalRadius = 3f;
	private const float TargetMovementThreshold = 2f;
	private const float MinimumProgress = 0.25f;
	private const int SegmentPoints = 8;
	private const int MaximumSegments = 64;
	private const int MaximumReplans = 3;
	private const int MaximumTargetWaits = 2;

	public static async Task<NaturalNavigationResult> ApproachNpcAsync(int mapId, int npcTemplateId,
		BotPosition staticAnchor, INaturalNavigationDriver driver, CancellationToken token = default)
	{
		ArgumentNullException.ThrowIfNull(driver);
		int routeSearches = 0, segments = 0, replans = 0, targetWaits = 0, sequence = 0;
		int? targetId = null;
		BotPosition destination = staticAnchor;
		IReadOnlyList<BotPosition> route = [];
		int routeIndex = 0;
		while (segments < MaximumSegments)
		{
			NaturalNavigationObservation observed = driver.Observe();
			if (observed.MapId != mapId || observed.Position is not BotPosition start || observed.IsDead)
				return Fail("Map, position, or survival state changed during navigation.", observed);
			NaturalNavigationObject? target = observed.Npcs
				.Where(npc => npc.TemplateId == npcTemplateId)
				.OrderBy(npc => Distance(start, npc.Position)).ThenBy(npc => npc.ObjectId).FirstOrDefault();
			if (target != null)
			{
				if (Distance(start, target.Position) <= ArrivalRadius)
				{
					Emit("navigation-arrived", "completed", "Within interaction approach radius of a client-observed NPC.",
						start, target.Position, target.ObjectId);
					return new(true, "Observed NPC approached; no quest interaction performed.", target.ObjectId, routeSearches, segments);
				}
				if (targetId != target.ObjectId || Distance(destination, target.Position) > TargetMovementThreshold)
				{
					if (targetId != null && ++replans > MaximumReplans)
						return Fail("Moving/replaced target exceeded the bounded replan budget.", observed);
					targetId = target.ObjectId;
					destination = target.Position;
					route = [];
					Emit("target-reacquired", "planned", "Using the latest client-observed NPC object and position.",
						start, destination, targetId);
				}
			}
			else if (targetId != null)
			{
				if (++targetWaits > MaximumTargetWaits)
					return Fail("Observed target disappeared and did not reappear within the bounded wait.", observed);
				Emit("target-lost", "planned", "NPC disappeared; waiting for another observed instance.", start, destination, targetId);
				await driver.SynchronizeAsync(token);
				continue;
			}
			else if (Distance(start, staticAnchor) <= ArrivalRadius)
			{
				if (++targetWaits > MaximumTargetWaits)
					return Fail("Reached the static area anchor but no NPC was observed.", observed);
				Emit("await-observed-target", "planned", "Area anchor reached; only client packets may identify the NPC.",
					start, staticAnchor, null);
				await driver.SynchronizeAsync(token);
				continue;
			}

			if (route.Count == 0 || routeIndex >= route.Count)
			{
				route = await driver.FindRouteAsync(start, destination, token);
				routeSearches++;
				routeIndex = 0;
				if (route.Count == 0)
					return Fail("No collision-checked route to the current destination.", observed);
				Emit(targetId == null ? "route-to-anchor" : "route-to-observed-npc", "planned",
					$"Collision-checked path has {route.Count} points; travel is segmented and speed-paced.",
					start, destination, targetId, route.ToArray());
			}
			BotPosition[] segment = route.Skip(routeIndex).Take(SegmentPoints).ToArray();
			BotPosition expected = segment[^1];
			await driver.MoveAsync(segment, token);
			segments++;
			await driver.SynchronizeAsync(token);
			NaturalNavigationObservation after = driver.Observe();
			if (after.MapId != mapId || after.Position is not BotPosition actual || after.IsDead)
				return Fail("Map, position, or survival state changed after a movement segment.", after);
			float progress = Distance(start, actual);
			if (progress < MathF.Min(MinimumProgress, Distance(start, expected) * 0.5f) ||
				Distance(actual, expected) > ArrivalRadius)
			{
				if (++replans > MaximumReplans)
					return Fail("Client position did not advance as planned after bounded replans.", after);
				route = [];
				Emit("replan-stalled", "planned", $"Client-position progress {progress:F2} m; replanning from the latest client position.",
					actual, destination, targetId);
				continue;
			}
			routeIndex += segment.Length;
			Emit("segment-progress", "completed", $"Client-estimated progress {progress:F2} m; not a server movement echo.",
				actual, destination, targetId);
		}
		return Fail("Navigation exceeded the bounded segment budget.", driver.Observe());

		NaturalNavigationResult Fail(string reason, NaturalNavigationObservation state)
		{
			Emit("navigation-failed", "blocked", reason, state.Position, destination, targetId);
			return new(false, reason, targetId, routeSearches, segments);
		}
		void Emit(string action, string outcome, string reason, BotPosition? position, BotPosition goal,
			int? objectId, BotPosition[]? plannedRoute = null) =>
			driver.Record(new NaturalNavigationEvent(++sequence, action, outcome, reason, mapId, npcTemplateId,
				position, goal, objectId, routeSearches, segments, plannedRoute));
	}

	private static float Distance(BotPosition left, BotPosition right) => MathF.Sqrt(
		MathF.Pow(left.X - right.X, 2) + MathF.Pow(left.Y - right.Y, 2) + MathF.Pow(left.Z - right.Z, 2));
}
