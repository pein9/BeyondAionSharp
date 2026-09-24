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
	bool IsSegmentSafe(IReadOnlyList<BotPosition> segment, int? targetObjectId) => true;
	void Record(NaturalNavigationEvent navigationEvent);
}

/// <summary>Client-observed, bounded ground approach. Its position is a paced client estimate, not a server echo.</summary>
public static class NaturalIshalgenNavigator
{
	/// <summary>Thin actual client-estimated progress into reverse-route checkpoints.
	/// Planned routes are intentionally excluded: only walked positions may guide
	/// a return, and every reverse leg is routed and hazard-checked again.</summary>
	public static BotPosition[] SelectRetraceCheckpoints(
		IEnumerable<NaturalNavigationEvent> events, int stride = 2)
	{
		ArgumentNullException.ThrowIfNull(events);
		if (stride <= 0) throw new ArgumentOutOfRangeException(nameof(stride));
		BotPosition[] walked = events
			.Where(item => item.Action == "segment-progress" && item.Position != null)
			.Select(item => item.Position!.Value).ToArray();
		return walked.Where((_, index) => index % stride == 0)
			.Concat(walked.TakeLast(1)).Distinct().ToArray();
	}

	private const float ArrivalRadius = 3f;
	private const float TargetMovementThreshold = 2f;
	private const float MinimumProgress = 0.25f;
	private const int SegmentPoints = 8;
	// Each segment is 8 checked points (about 16 m) of real progress, so this is a stall guard, not a
	// distance limit: 1,000 segments is about 16 km, several times the longest single walk on a map.
	private const int MaximumSegments = 1000;
	private const int MaximumReplans = 3;
	private const int MaximumTargetWaits = 2;

	public static async Task<NaturalNavigationResult> ApproachNpcAsync(int mapId, int npcTemplateId,
		BotPosition staticAnchor, INaturalNavigationDriver driver, CancellationToken token = default)
		=> await ApproachObservedObjectAsync(mapId, npcTemplateId, staticAnchor, driver, "NPC", token);

	public static async Task<NaturalNavigationResult> ApproachObservedObjectAsync(int mapId, int templateId,
		BotPosition staticAnchor, INaturalNavigationDriver driver, string kind, CancellationToken token = default)
		=> await ApproachAsync(mapId, templateId, staticAnchor, driver, kind, false, ArrivalRadius, token);

	public static async Task<NaturalNavigationResult> ExploreAnchorAsync(int mapId, int templateId,
		BotPosition staticAnchor, INaturalNavigationDriver driver, string kind, CancellationToken token = default)
		=> await ApproachAsync(mapId, templateId, staticAnchor, driver, kind, true, ArrivalRadius, token);

	/// <summary>Reach ordinary spell/search range of a shipped area hint without claiming the target exists.</summary>
	public static async Task<NaturalNavigationResult> ExploreWithinRangeAsync(int mapId, int templateId,
		BotPosition staticAnchor, float radius, INaturalNavigationDriver driver, string kind,
		CancellationToken token = default)
	{
		if (!float.IsFinite(radius) || radius <= 0)
			throw new ArgumentOutOfRangeException(nameof(radius));
		return await ApproachAsync(mapId, templateId, staticAnchor, driver, kind, true, radius, token);
	}

	/// <summary>Return over client-estimated checkpoints from a previously checked approach.
	/// Each reverse leg is independently routed against current observations. A checkpoint
	/// occupied by a newly observed hostile may be skipped only if a farther recorded
	/// checkpoint has its own checked, hazard-safe route.</summary>
	public static async Task<NaturalNavigationResult> RetraceIngressAsync(int mapId,
		BotPosition ingressStart, IReadOnlyList<BotPosition> checkpoints,
		INaturalNavigationDriver driver, CancellationToken token = default)
	{
		ArgumentNullException.ThrowIfNull(checkpoints);
		ArgumentNullException.ThrowIfNull(driver);
		int searches = 0, segments = 0, skipped = 0;
		var skippedReasons = new List<string>();
		BotPosition[] returnPoints = checkpoints.Reverse().Append(ingressStart).ToArray();
		for (int index = 0; index < returnPoints.Length; index++)
		{
			BotPosition checkpoint = returnPoints[index];
			BotPosition? current = driver.Observe().Position;
			if (current is BotPosition position && Distance(position, checkpoint) <= ArrivalRadius) continue;
			NaturalNavigationResult leg = await ExploreAnchorAsync(mapId, -1, checkpoint,
				driver, "recorded-ingress-checkpoint", token);
			searches += leg.RouteSearches;
			segments += leg.Segments;
			if (!leg.Arrived)
			{
				skippedReasons.Add($"{checkpoint}: {leg.Reason}");
				// Recorded ingress length, not an arbitrary number of newly occupied
				// checkpoints, bounds this search. A farther checkpoint may be the
				// first one outside a hostile pack and still have a checked detour.
				skipped++;
				if (index == returnPoints.Length - 1)
					return new(false, $"Checked return could not bypass {skipped} blocked ingress " +
						$"checkpoints: {string.Join(" | ", skippedReasons)}", null, searches, segments);
				continue;
			}
			skipped = 0;
			skippedReasons.Clear();
		}
		return new(true, "Returned along checked ingress checkpoints.", null, searches, segments);
	}

	private static async Task<NaturalNavigationResult> ApproachAsync(int mapId, int templateId,
		BotPosition staticAnchor, INaturalNavigationDriver driver, string kind, bool allowAnchorOnly,
		float arrivalRadius, CancellationToken token)
	{
		ArgumentNullException.ThrowIfNull(driver);
		int routeSearches = 0, segments = 0, replans = 0, targetWaits = 0, targetMoves = 0, sequence = 0;
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
				.Where(npc => npc.TemplateId == templateId)
				.OrderBy(npc => Distance(start, npc.Position)).ThenBy(npc => npc.ObjectId).FirstOrDefault();
			if (target != null)
			{
				if (Distance(start, target.Position) <= arrivalRadius)
				{
					Emit("navigation-arrived", "completed", $"Within {arrivalRadius:F1} m of a client-observed {kind}.",
						start, target.Position, target.ObjectId);
					return new(true, $"Observed {kind} approached; no interaction performed.", target.ObjectId, routeSearches, segments);
				}
				if (targetId != target.ObjectId || Distance(destination, target.Position) > TargetMovementThreshold)
				{
					// Following a walking NPC is ordinary travel, as a player keeps walking toward it: every
					// iteration still moves a checked segment, so the segment guard bounds this, not the
					// hazard replan budget.
					if (targetId != null && ++targetMoves > MaximumSegments)
						return Fail("Moving/replaced target exceeded the bounded replan budget.", observed);
					targetId = target.ObjectId;
					destination = target.Position;
					route = [];
					Emit("target-reacquired", "planned", $"Using the latest client-observed {kind} object and position.",
						start, destination, targetId);
				}
			}
			else if (targetId != null)
			{
				if (++targetWaits > MaximumTargetWaits)
				{
					// Usually the edge of the visibility range, not a despawn: walk on to the shipped spawn
					// hint as a player would, and reacquire the NPC when it comes back into view. If it is
					// really gone, the anchor branch below reports that on arrival.
					Emit("target-out-of-view", "planned", $"{kind} left view; continuing to its shipped area hint.",
						start, staticAnchor, targetId);
					targetId = null;
					targetWaits = 0;
					destination = staticAnchor;
					route = [];
					continue;
				}
				Emit("target-lost", "planned", $"{kind} disappeared; waiting for another observed instance.", start, destination, targetId);
				await driver.SynchronizeAsync(token);
				continue;
			}
			else if (Distance(start, staticAnchor) <= arrivalRadius)
			{
				if (allowAnchorOnly)
				{
					Emit("anchor-observed", "completed", $"Reached the shipped {kind} area hint; rescan client-visible objects.",
						start, staticAnchor, null);
					return new(true, "Reached area hint without assuming a gatherable is present.", null, routeSearches, segments);
				}
				if (++targetWaits > MaximumTargetWaits)
					return Fail($"Reached the static area anchor but no {kind} was observed.", observed);
				Emit("await-observed-target", "planned", $"Area anchor reached; only client packets may identify the {kind}.",
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
				Emit(targetId == null ? "route-to-anchor" : kind == "NPC" ? "route-to-observed-npc" : "route-to-observed-object", "planned",
					$"Collision-checked path has {route.Count} points; travel is segmented and speed-paced.",
					start, destination, targetId, route.ToArray());
			}
			BotPosition[] segment = route.Skip(routeIndex).Take(SegmentPoints).ToArray();
			BotPosition expected = segment[^1];
			if (!driver.IsSegmentSafe(segment, targetId))
			{
				if (++replans > MaximumReplans)
					return Fail("New client-observed hazards exceeded the bounded replan budget.", observed);
				route = [];
				Emit("replan-hostile", "planned", "A newly observed hostile lies on the next ground segment.",
					start, destination, targetId);
				continue;
			}
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
			// The replan budget counts replans in a row without progress (a real stall), not every new
			// hostile met on a long walk that keeps advancing.
			replans = 0;
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
			driver.Record(new NaturalNavigationEvent(++sequence, action, outcome, reason, mapId, templateId,
				position, goal, objectId, routeSearches, segments, plannedRoute));
	}

	private static float Distance(BotPosition left, BotPosition right) => MathF.Sqrt(
		MathF.Pow(left.X - right.X, 2) + MathF.Pow(left.Y - right.Y, 2) + MathF.Pow(left.Z - right.Z, 2));
}
