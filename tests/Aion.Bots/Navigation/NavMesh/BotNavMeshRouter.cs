using Aion.Bots.World;

namespace Aion.Bots.Navigation.NavMesh;

/// <summary>Why the last navmesh route request ended the way it did (for traces and tests).</summary>
public enum BotNavRouteOutcome
{
	Routed,
	NoNavMesh,
	EndpointOffMesh,
	NotConnected,
	GeometryRejected,
	HazardRejected,
	NoApproachPoint,
}

/// <summary>
/// Plans on a baked navmesh and emits only collision-checked ground samples. Every consecutive pair of
/// returned points has passed <see cref="BotNavigationGeometry.TraceEdge"/>, exactly as with the grid
/// search, so the "never emit an unchecked edge" rule is unchanged; the navmesh only decides where to go.
/// A step the server geometry rejects is sidestepped or repaired with a short grid search, and an
/// unrepairable leg fails the whole route rather than being emitted unchecked.
/// </summary>
public sealed class BotNavMeshRouter(BotNavMeshSet navMeshes, BotNavigationGeometry geometry)
{
	// Just under the ground check's two-metre sample spacing: a step of exactly 2 m can round to 2.0000002
	// and be split into two 1 m samples, halving the distance callers cover per fixed point count.
	private const float StepLength = BotNavigationGeometry.SampleSpacing * 0.995f;
	private const float InteractionRadius = 3f;
	private const float RangedRadius = 20f;
	private const float RepairLimit = 12f;
	private const int CornerSkip = 3;
	private const float BridgeLimit = 40f;
	/// <summary>Extra room around a blocked window, tried in turn: packs can close the tight corridor.</summary>
	private static readonly float[] DetourMargins = [0, 40, 100];

	[ThreadStatic] private static BotNavRouteOutcome lastOutcome;

	public BotNavMeshSet NavMeshes { get; } = navMeshes;
	public BotNavigationGeometry Geometry { get; } = geometry;
	public static BotNavRouteOutcome LastOutcome => lastOutcome;

	/// <summary>Optional diagnostic sink for rejected legs (tools and focused tests only).</summary>
	public Action<string>? Log { get; set; }

	public bool Covers(int mapId) => NavMeshes.Get(mapId) != null;

	/// <summary>Checked route ending exactly at <paramref name="destination"/>.</summary>
	public IReadOnlyList<BotPosition> FindPath(int mapId, BotPosition start, BotPosition destination, BotNavQuery? options = null)
	{
		options ??= BotNavQuery.Default;
		if (options.Hazards.Count > 0 && options.Hazards.Any(hazard =>
			Horizontal(start, hazard.Position) >= hazard.Radius && Horizontal(destination, hazard.Position) < hazard.Radius))
		{
			Log?.Invoke($"destination {destination} lies inside an observed hazard");
			return Fail(BotNavRouteOutcome.HazardRejected);
		}
		if (Distance(start, destination) <= 0.05f) return Succeed([]);
		IReadOnlyList<BotPosition>? route = Route(mapId, start, destination, options);
		if (route == null && lastOutcome is BotNavRouteOutcome.NotConnected or BotNavRouteOutcome.EndpointOffMesh)
			route = StepOffIsland(mapId, start, destination, options, (from, _) => Route(mapId, from, destination, options))
				?? Bridge(mapId, start, destination, options);
		if (route == null) return [];
		if (route.Count == 0 || Distance(route[^1], destination) > 0.05f)
		{
			BotPosition from = route.Count == 0 ? start : route[^1];
			IReadOnlyList<BotPosition>? tail = CheckedLeg(mapId, from, destination, options.Hazards, start, route);
			if (tail == null) return Fail(BotNavRouteOutcome.GeometryRejected);
			route = [.. route, .. tail];
			if (options.Hazards.Count > 0 && !BotNavigationGeometry.AvoidsHazards(start, route, options.Hazards))
			{
				Log?.Invoke($"exact tail {from} -> {destination} breaks the hazard rule");
				return Fail(BotNavRouteOutcome.HazardRejected);
			}
		}
		return Succeed(route);
	}

	/// <summary>The bot itself stands on a separate scrap of navmesh (a rock top, a crate, a ledge the voxel
	/// bake split off) that the server geometry let it reach: take a short checked step to nearby ground on
	/// the destination's island (within 15 m), then continue with <paramref name="onward"/>. Null when no such
	/// step exists.</summary>
	private IReadOnlyList<BotPosition>? StepOffIsland(int mapId, BotPosition start, BotPosition destination, BotNavQuery options,
		Func<BotPosition, BotNavRouteOutcome, IReadOnlyList<BotPosition>?> onward)
	{
		BotNavRouteOutcome original = lastOutcome;
		BotNavMesh? mesh = NavMeshes.Get(mapId);
		if (mesh == null) return null;
		int here = mesh.IslandOf(start, options with { SnapHorizontal = 2 });
		int there = mesh.IslandOf(destination, options);
		if (there < 0 || here == there) { lastOutcome = original; return null; }
		foreach (BotPosition ground in Around(start, [2f, 4f, 7f, 10f, 15f], 12)
			.Select(p => mesh.Snap(p, options with { SnapHorizontal = 1.5f, SnapVertical = 15f })).OfType<BotPosition>()
			.Where(p => mesh.IslandOf(p, options with { SnapHorizontal = 1f }) == there)
			.OrderBy(p => Horizontal(p, start)).Take(6))
		{
			IReadOnlyList<BotPosition> step = options.Hazards.Count == 0
				? Geometry.GridLocalPath(mapId, start, ground)
				: Geometry.GridJourneyPathAvoiding(mapId, start, ground, options.Hazards);
			if (step.Count == 0) continue;
			IReadOnlyList<BotPosition>? rest = onward(step[^1], lastOutcome);
			if (rest == null) continue;
			List<BotPosition> route = [.. step, .. rest];
			if (options.Hazards.Count > 0 && !BotNavigationGeometry.AvoidsHazards(start, route, options.Hazards)) continue;
			Log?.Invoke($"stepped off navmesh island {here} at {start} onto {there} at {step[^1]}");
			lastOutcome = BotNavRouteOutcome.Routed;
			return route;
		}
		lastOutcome = original;
		return null;
	}

	/// <summary>The destination stands on a separate scrap of navmesh (a crate top, a platform, a ledge the voxel
	/// bake split off) or off the mesh: walk to the reachable point closest to it, then let the bounded grid search
	/// settle the last few metres. Null when that point is still too far.</summary>
	private IReadOnlyList<BotPosition>? Bridge(int mapId, BotPosition start, BotPosition destination, BotNavQuery options)
	{
		BotNavRouteOutcome original = lastOutcome;
		IReadOnlyList<BotPosition>? partial = Route(mapId, start, destination, options, allowPartial: true, acceptShort: true);
		if (partial == null) { lastOutcome = original; return null; }
		BotPosition end = partial.Count == 0 ? start : partial[^1];
		if (Horizontal(end, destination) > BridgeLimit) { lastOutcome = original; return null; }
		IReadOnlyList<BotPosition> tail = options.Hazards.Count == 0
			? Geometry.GridLocalPath(mapId, end, destination)
			: Geometry.GridJourneyPathAvoiding(mapId, end, destination, options.Hazards);
		if (tail.Count == 0) { lastOutcome = original; return null; }
		List<BotPosition> route = [.. partial, .. tail];
		if (options.Hazards.Count > 0 && !BotNavigationGeometry.AvoidsHazards(start, route, options.Hazards))
		{
			Log?.Invoke($"bridge tail {end} -> {destination} breaks the hazard rule");
			lastOutcome = BotNavRouteOutcome.HazardRejected;
			return null;
		}
		lastOutcome = BotNavRouteOutcome.Routed;
		return route;
	}

	/// <summary>Checked route to any ground within the ordinary 3 m interaction radius of <paramref name="target"/>.
	/// NPCs often stand inside collision or off the eroded mesh, so the route ends at the nearest walkable
	/// point and, if that is still too far, one short checked hop onto ground around the target.</summary>
	public IReadOnlyList<BotPosition> FindInteractionPath(int mapId, BotPosition start, BotPosition target, BotNavQuery? options = null)
	{
		options ??= BotNavQuery.Default;
		if (Distance(start, target) <= InteractionRadius) return Succeed([]);
		BotNavMesh? mesh = NavMeshes.Get(mapId);
		if (mesh == null) return Fail(BotNavRouteOutcome.NoNavMesh);
		// Partial: the target may stand on its own tiny island (a platform, a crate) or snap to a rock top;
		// Detour then walks to the reachable polygon closest to it, which usually lies within reach.
		IReadOnlyList<BotPosition>? route = Route(mapId, start, target, options, allowPartial: true, acceptShort: true);
		if (route == null && lastOutcome is BotNavRouteOutcome.NotConnected or BotNavRouteOutcome.EndpointOffMesh)
			route = StepOffIsland(mapId, start, target, options,
				(from, _) => Route(mapId, from, target, options, allowPartial: true, acceptShort: true));
		if (route == null)
		{
			BotNavRouteOutcome first = lastOutcome;
			foreach (BotPosition candidate in Around(target, [1.5f, 2.5f], 12)
				.Select(p => mesh.Snap(p, options with { SnapHorizontal = 1f, SnapVertical = 3f }))
				.OfType<BotPosition>().Where(p => Distance(p, target) <= InteractionRadius)
				.OrderBy(p => Distance(p, start)).Take(3))
			{
				route = Route(mapId, start, candidate, options, acceptShort: true);
				if (route != null) break;
			}
			if (route == null) return Fail(first);
		}
		BotPosition end = route.Count == 0 ? start : route[^1];
		if (Distance(end, target) <= InteractionRadius) return Succeed(route);
		foreach (BotPosition candidate in Around(target, [1.5f, 2.5f], 12).OrderBy(p => Horizontal(p, end)))
		{
			if (Horizontal(candidate, end) > 8) break;
			IReadOnlyList<BotPosition>? hop = CheckedLeg(mapId, end, candidate with { Z = end.Z }, options.Hazards, start, route, mesh);
			if (hop == null || hop.Count == 0 || Distance(hop[^1], target) > InteractionRadius) continue;
			return Succeed([.. route, .. hop]);
		}
		return Fail(BotNavRouteOutcome.NoApproachPoint);
	}

	/// <summary>Checked route to ground within spell range of <paramref name="target"/> with line of sight.
	/// The target's own position is not the goal; other hazards stay avoided.</summary>
	public IReadOnlyList<BotPosition> FindRangedApproachPath(int mapId, BotPosition start, BotPosition target,
		BotNavQuery? options = null, float range = RangedRadius)
	{
		options ??= BotNavQuery.Default;
		if (Distance(start, target) <= range && Geometry.HasLineOfSight(mapId, start, target)) return Succeed([]);
		BotNavMesh? mesh = NavMeshes.Get(mapId);
		if (mesh == null) return Fail(BotNavRouteOutcome.NoNavMesh);
		IReadOnlyList<BotPosition>? toward = Route(mapId, start, target, options, allowPartial: true, acceptShort: true);
		if (toward != null)
			for (int i = 0; i < toward.Count; i++)
				if (Distance(toward[i], target) <= range && Geometry.HasLineOfSight(mapId, toward[i], target))
					return Succeed(toward.Take(i + 1).ToArray());
		foreach (BotPosition ground in Around(target, [range * 0.5f, range * 0.75f, range * 0.9f], 16)
			.Select(p => mesh.Snap(p, options with { SnapHorizontal = 2f, SnapVertical = 8f })).OfType<BotPosition>()
			.Where(p => Distance(p, target) <= range).OrderBy(p => Distance(p, start))
			.Where(p => Geometry.HasLineOfSight(mapId, p, target)).Take(6))
		{
			IReadOnlyList<BotPosition>? path = Route(mapId, start, ground, options);
			if (path != null && path.Count > 0) return Succeed(path);
		}
		return Fail(BotNavRouteOutcome.NoApproachPoint);
	}

	/// <summary>Navmesh corners densified to checked 2 m ground samples, or null on failure (outcome recorded).
	/// Corner heights are projected onto the detail surface (Detour corners carry coarse polygon-vertex
	/// heights). A corner the server geometry rejects (typically a boundary vertex on a small steep face)
	/// is skipped in favour of a later one. With <paramref name="acceptShort"/> a route whose final corner
	/// cannot be reached still returns the checked prefix, for callers that only need to get close.</summary>
	private IReadOnlyList<BotPosition>? Route(int mapId, BotPosition start, BotPosition destination, BotNavQuery options,
		bool allowPartial = false, bool acceptShort = false)
	{
		BotNavMesh? mesh = NavMeshes.Get(mapId);
		if (mesh == null) { lastOutcome = BotNavRouteOutcome.NoNavMesh; return null; }
		if (options.HazardClearance > 0 && options.Hazards.Count > 0)
			options = options with
			{
				HazardClearance = 0,
				Danger = [.. options.Danger, .. options.Hazards
					.Where(h => Horizontal(start, h.Position) >= h.Radius + options.HazardClearance &&
						Horizontal(destination, h.Position) >= h.Radius + options.HazardClearance)
					.Select(h => new BotNavDanger(h.Position.X, h.Position.Y, h.Radius + options.HazardClearance, 8))],
			};
		if (mesh.Snap(start, options) == null || (!allowPartial && mesh.Snap(destination, options) == null))
		{ lastOutcome = BotNavRouteOutcome.EndpointOffMesh; return null; }
		IReadOnlyList<BotPosition> raw = mesh.FindCorners(start, destination, options, allowPartial);
		if (raw.Count == 0) { lastOutcome = BotNavRouteOutcome.NotConnected; return null; }
		BotPosition[] corners = raw.Select(c => mesh.HeightAt(c.X, c.Y, c.Z) is float z && float.IsFinite(z) ? c with { Z = z } : c).ToArray();
		var samples = new List<BotPosition>();
		BotPosition previous = start;
		int index = 0;
		while (index < corners.Length)
		{
			IReadOnlyList<BotPosition>? leg = null;
			int reached = -1;
			for (int candidate = index; candidate < Math.Min(corners.Length, index + CornerSkip) && leg == null; candidate++)
			{
				leg = CheckedLeg(mapId, previous, corners[candidate], [], start, samples, mesh);
				if (leg != null) reached = candidate;
			}
			if (leg == null)
			{
				Log?.Invoke($"corner {index}/{corners.Length} {corners[index]} unreachable from {previous}");
				lastOutcome = BotNavRouteOutcome.GeometryRejected;
				if (!acceptShort || samples.Count == 0) return null;
				break;
			}
			samples.AddRange(leg);
			if (leg.Count > 0) previous = leg[^1];
			index = reached + 1;
		}
		bool complete = index >= corners.Length;
		if (options.Hazards.Count > 0 || options.Danger.Count > 0)
		{
			List<BotPosition>? resolved = ResolveZones(mapId, mesh, start, samples, options);
			if (resolved == null)
			{
				lastOutcome = BotNavRouteOutcome.HazardRejected;
				return null;
			}
			samples = resolved;
		}
		lastOutcome = complete ? BotNavRouteOutcome.Routed : BotNavRouteOutcome.GeometryRejected;
		return samples;
	}

	/// <summary>
	/// Detour chooses polygon corridors, and one large polygon can contain a whole aggro circle, so costs alone
	/// cannot bend a route around it. This pass finds where the checked route first breaks the observed-hazard
	/// rule (or enters a heavily weighted danger zone), re-plans that window on a one-metre grid laid over the
	/// navmesh surface with the circles forbidden (or costed), re-checks every new step against the server
	/// geometry, and repeats. Hard hazards must end up satisfied; soft danger is best effort.
	/// </summary>
	private List<BotPosition>? ResolveZones(int mapId, BotNavMesh mesh, BotPosition start, List<BotPosition> samples, BotNavQuery options)
	{
		var result = samples;
		BotNavDanger[] heavy = options.Danger.Where(d => d.Weight >= 8 &&
			Horizontal(start, new BotPosition(d.X, d.Y, 0, 0)) >= d.Radius).ToArray();
		for (int pass = 0; pass < 10; pass++)
		{
			int bad = FirstHazardViolation(start, result, options.Hazards);
			int soft = bad >= 0 ? -1 : result.FindIndex(p => heavy.Any(d => InZone(p, d)));
			int at = bad >= 0 ? bad : soft;
			if (at < 0) return result;
			if (soft >= 0 && heavy.Any(d => InZone(result[^1], d))) return result; // the goal itself is in the zone
			int from = Math.Max(-1, at - 5);
			int to = ExitIndex(result, at, start, options.Hazards, heavy);
			BotPosition a = from < 0 ? start : result[from];
			BotPosition b = result[to];
			List<BotPosition> prefix = result.Take(from + 1).ToList();
			List<BotPosition>? detour = null;
			foreach (float extra in DetourMargins)
			{
				detour = LocalDetour(mapId, mesh, a, b, start, prefix, options.Hazards, heavy, bad >= 0, extra);
				if (detour != null) break;
			}
			if (detour == null)
			{
				Log?.Invoke($"no local detour {a} -> {b} around {(bad >= 0 ? "observed hazards" : "danger")}");
				return bad >= 0 ? null : result;
			}
			result = [.. prefix, .. detour, .. result.Skip(to + 1)];
		}
		if (FirstHazardViolation(start, result, options.Hazards) < 0) return result;
		Log?.Invoke("hazard violations remain after 10 local detours");
		return null;
	}

	/// <summary>Index of the first sample at which <see cref="BotNavigationGeometry.AvoidsHazards"/> fails, or -1.</summary>
	private static int FirstHazardViolation(BotPosition start, List<BotPosition> route, IReadOnlyList<BotNavigationHazard> hazards)
	{
		if (hazards.Count == 0 || BotNavigationGeometry.AvoidsHazards(start, route, hazards)) return -1;
		int low = 0, high = route.Count - 1; // prefix [0..high] fails; find the smallest failing prefix end
		while (low < high)
		{
			int mid = (low + high) / 2;
			if (BotNavigationGeometry.AvoidsHazards(start, route.GetRange(0, mid + 1), hazards)) low = mid + 1;
			else high = mid;
		}
		return low;
	}

	/// <summary>First sample from <paramref name="at"/> on after which three samples in a row are clear of every
	/// relevant zone (hazards that do not contain the route start, and heavy danger), or the last sample.</summary>
	private static int ExitIndex(List<BotPosition> route, int at, BotPosition start, IReadOnlyList<BotNavigationHazard> hazards,
		BotNavDanger[] heavy)
	{
		BotNavigationHazard[] relevant = hazards.Where(h => Horizontal(start, h.Position) >= h.Radius).ToArray();
		bool Clear(BotPosition p) => relevant.All(h => Horizontal(p, h.Position) >= h.Radius + 0.5f) && heavy.All(d => !InZone(p, d));
		for (int j = at + 1; j < route.Count; j++)
			if (Clear(route[j]) && (j + 1 >= route.Count || Clear(route[j + 1])) && (j + 2 >= route.Count || Clear(route[j + 2])))
				return j;
		return route.Count - 1;
	}

	/// <summary>A* on a one-metre grid over the navmesh surface from <paramref name="a"/> to <paramref name="b"/>.
	/// Cells must lie on a walkable polygon; hazards that do not contain the route start are forbidden, those that
	/// do cost extra (the bot may only work its way out); heavy danger multiplies cost. Every emitted step is then
	/// re-checked against the server geometry.</summary>
	private List<BotPosition>? LocalDetour(int mapId, BotNavMesh mesh, BotPosition a, BotPosition b, BotPosition routeStart,
		List<BotPosition> prefix, IReadOnlyList<BotNavigationHazard> hazards, BotNavDanger[] heavy, bool hard, float extraMargin)
	{
		const float Cell = 1f;
		float margin = extraMargin + 12 + hazards.Concat(heavy.Select(d => new BotNavigationHazard(new BotPosition(d.X, d.Y, 0, 0), d.Radius)))
			.Where(h => Horizontal(h.Position, a) < 200 || Horizontal(h.Position, b) < 200)
			.Select(h => h.Radius).DefaultIfEmpty(0).Max() * 2;
		float minX = MathF.Min(a.X, b.X) - margin, maxX = MathF.Max(a.X, b.X) + margin;
		float minY = MathF.Min(a.Y, b.Y) - margin, maxY = MathF.Max(a.Y, b.Y) + margin;
		if (maxX - minX > 420 || maxY - minY > 420) return null;
		BotNavigationHazard[] forbidden = hazards.Where(h => Horizontal(routeStart, h.Position) >= h.Radius).ToArray();
		BotNavigationHazard[] escaping = hazards.Where(h => Horizontal(routeStart, h.Position) < h.Radius).ToArray();
		var open = new PriorityQueue<(int X, int Y), float>();
		var cost = new Dictionary<(int, int), float>();
		var parent = new Dictionary<(int, int), (int, int)>();
		var height = new Dictionary<(int, int), float>();
		var surface = new Dictionary<(int, int), float>();
		(int, int) origin = (0, 0);
		cost[origin] = 0;
		height[origin] = a.Z;
		open.Enqueue(origin, Horizontal(a, b));
		(int, int)? goal = null;
		int expanded = 0;
		while (open.TryDequeue(out var cell, out _) && expanded++ < 150000)
		{
			float cx = a.X + cell.X * Cell, cy = a.Y + cell.Y * Cell, cz = height[cell];
			if (Horizontal(new BotPosition(cx, cy, cz, 0), b) <= 1.5f && MathF.Abs(cz - b.Z) < 2.5f) { goal = cell; break; }
			for (int dx = -1; dx <= 1; dx++)
				for (int dy = -1; dy <= 1; dy++)
				{
					if (dx == 0 && dy == 0) continue;
					var next = (cell.X + dx, cell.Y + dy);
					float nx = a.X + next.Item1 * Cell, ny = a.Y + next.Item2 * Cell;
					if (nx < minX || nx > maxX || ny < minY || ny > maxY) continue;
					if (!surface.TryGetValue(next, out float nz))
						surface[next] = nz = mesh.HeightAt(nx, ny, cz);
					float step = MathF.Sqrt(dx * dx + dy * dy) * Cell;
					if (!float.IsFinite(nz) || MathF.Abs(nz - cz) > step * 1.2f) continue;
					var point = new BotPosition(nx, ny, nz, 0);
					if (forbidden.Any(h => Horizontal(point, h.Position) < h.Radius + 0.5f)) continue;
					float factor = 1;
					foreach (BotNavigationHazard h in escaping)
						if (Horizontal(point, h.Position) < h.Radius) factor += 20;
					foreach (BotNavDanger d in heavy)
						if (InZone(point, d)) factor = MathF.Max(factor, d.Weight);
					float candidate = cost[cell] + step * factor;
					if (cost.TryGetValue(next, out float known) && known <= candidate) continue;
					cost[next] = candidate;
					parent[next] = cell;
					height[next] = nz;
					open.Enqueue(next, candidate + Horizontal(point, b));
				}
		}
		if (goal == null) return null;
		var cells = new List<(int, int)>();
		for (var c = goal.Value; ; c = parent[c])
		{
			cells.Add(c);
			if (!parent.ContainsKey(c)) break;
		}
		cells.Reverse();
		// Thin to roughly two-metre waypoints (the leg check samples every two metres anyway).
		var waypoints = new List<BotPosition>();
		for (int i = 2; i < cells.Count; i += 2)
			waypoints.Add(new BotPosition(a.X + cells[i].Item1 * Cell, a.Y + cells[i].Item2 * Cell, height[cells[i]], b.Heading));
		waypoints.Add(b);
		var detour = new List<BotPosition>();
		BotPosition previous = a;
		foreach (BotPosition waypoint in waypoints)
		{
			if (Horizontal(previous, waypoint) < 0.05f) continue;
			IReadOnlyList<BotPosition>? leg = CheckedLeg(mapId, previous, waypoint, [], routeStart, [.. prefix, .. detour], mesh);
			if (leg == null || leg.Count == 0) return null;
			detour.AddRange(leg);
			previous = leg[^1];
		}
		if (hard && !BotNavigationGeometry.AvoidsHazards(routeStart, [.. prefix, .. detour], hazards)) return null;
		return detour;
	}

	private static bool InZone(BotPosition p, BotNavDanger d) => (p.X - d.X) * (p.X - d.X) + (p.Y - d.Y) * (p.Y - d.Y) < d.Radius * d.Radius;

	/// <summary>Checked samples from <paramref name="from"/> to <paramref name="to"/>. The leg is walked in
	/// 2 m steps whose approximate height comes from the navmesh surface, because the ground check only
	/// searches two metres around a straight-line height. A rejected step first sidesteps (up to 1.5 m in
	/// any direction that still makes progress), then gets one short grid repair; otherwise the leg fails.</summary>
	private IReadOnlyList<BotPosition>? CheckedLeg(int mapId, BotPosition from, BotPosition to,
		IReadOnlyList<BotNavigationHazard> hazards, BotPosition routeStart, IReadOnlyList<BotPosition> emitted,
		BotNavMesh? mesh = null)
	{
		mesh ??= NavMeshes.Get(mapId);
		var samples = new List<BotPosition>();
		BotPosition previous = from;
		int guard = 0;
		while (Horizontal(previous, to) > 0.05f)
		{
			if (++guard > 4096) return null;
			float remaining = Horizontal(previous, to);
			float t = MathF.Min(1, StepLength / remaining);
			bool last = t >= 1;
			float x = previous.X + (to.X - previous.X) * t, y = previous.Y + (to.Y - previous.Y) * t;
			float z = last ? to.Z : previous.Z + (to.Z - previous.Z) * t;
			if (!last && mesh != null && mesh.HeightAt(x, y, z) is float surface && float.IsFinite(surface)) z = surface;
			var target = new BotPosition(x, y, z, to.Heading);
			IReadOnlyList<BotPosition>? edge = Accept(mapId, previous, target, hazards, routeStart, emitted, samples);
			bool sidestepped = false;
			if (edge == null && !last)
			{
				foreach (BotPosition side in Around(target, [0.5f, 1f, 1.5f], 8).OrderBy(p => Horizontal(p, to)))
				{
					if (Horizontal(side, to) >= remaining - 0.25f) continue;
					float sz = mesh?.HeightAt(side.X, side.Y, target.Z) is float h && float.IsFinite(h) ? h : target.Z;
					edge = Accept(mapId, previous, side with { Z = sz }, hazards, routeStart, emitted, samples);
					if (edge != null) { sidestepped = true; break; }
				}
			}
			if (edge == null)
			{
				IReadOnlyList<BotPosition>? repair = Repair(mapId, previous, to, hazards);
				if (repair != null && (hazards.Count == 0 ||
					BotNavigationGeometry.AvoidsHazards(routeStart, [.. emitted, .. samples, .. repair], hazards)))
				{
					samples.AddRange(repair);
					return samples;
				}
				Log?.Invoke($"step {previous} -> {target} (leg end {to}) rejected; sidestep and repair failed");
				return null;
			}
			samples.AddRange(edge);
			previous = edge[^1];
			if (last && !sidestepped) break;
		}
		return samples;
	}

	private IReadOnlyList<BotPosition>? Accept(int mapId, BotPosition previous, BotPosition target,
		IReadOnlyList<BotNavigationHazard> hazards, BotPosition routeStart, IReadOnlyList<BotPosition> emitted,
		IReadOnlyList<BotPosition> samples)
	{
		IReadOnlyList<BotPosition>? edge = Geometry.TraceEdge(mapId, previous, target);
		if (edge == null || edge.Count == 0) return null;
		if (hazards.Count > 0 && !BotNavigationGeometry.AvoidsHazards(routeStart, [.. emitted, .. samples, .. edge], hazards)) return null;
		return edge;
	}

	private IReadOnlyList<BotPosition>? Repair(int mapId, BotPosition from, BotPosition to, IReadOnlyList<BotNavigationHazard> hazards)
	{
		if (Horizontal(from, to) > RepairLimit) return null;
		IReadOnlyList<BotPosition> local = hazards.Count == 0
			? Geometry.GridLocalPath(mapId, from, to)
			: Geometry.GridJourneyPathAvoiding(mapId, from, to, hazards);
		return local.Count == 0 ? null : local;
	}

	private static IEnumerable<BotPosition> Around(BotPosition center, float[] radii, int sectors)
	{
		foreach (float radius in radii)
			for (int sector = 0; sector < sectors; sector++)
			{
				float angle = sector * 2 * MathF.PI / sectors;
				yield return center with { X = center.X + radius * MathF.Cos(angle), Y = center.Y + radius * MathF.Sin(angle) };
			}
	}

	private static IReadOnlyList<BotPosition> Succeed(IReadOnlyList<BotPosition> route)
	{
		lastOutcome = BotNavRouteOutcome.Routed;
		return route;
	}

	private static IReadOnlyList<BotPosition> Fail(BotNavRouteOutcome outcome)
	{
		lastOutcome = outcome;
		return [];
	}

	private static float Horizontal(BotPosition a, BotPosition b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
	private static float Distance(BotPosition a, BotPosition b) =>
		MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
}

/// <summary>Lazily loaded navmeshes, looked up in order in one or more directories (the checked-in
/// <c>game-server/data/nav</c> first, then the local all-maps cache <c>run/nav</c>).</summary>
public sealed class BotNavMeshSet
{
	private readonly Dictionary<int, BotNavMesh?> loaded = [];
	private readonly Lock gate = new();
	private readonly string[] directories;

	public BotNavMeshSet(params string[] directories)
	{
		if (directories.Length == 0) throw new ArgumentException("At least one navmesh directory is required.");
		this.directories = directories;
	}

	/// <summary>The first (authoritative) directory.</summary>
	public string Directory => directories[0];
	public IReadOnlyList<string> Directories => directories;

	/// <summary>The checked-in navmeshes: <c>game-server/data/nav</c> under the repository root.</summary>
	public static string DefaultDirectory(string repoRoot) => Path.Combine(repoRoot, "game-server", "data", "nav");

	/// <summary>Local, git-ignored navmeshes for every other map: <c>run/nav</c> under the repository root
	/// (<c>dotnet run --project tools/Aion.NavBake -- bake --maps all --out run/nav</c>).</summary>
	public static string LocalCacheDirectory(string repoRoot) => Path.Combine(repoRoot, "run", "nav");

	private static readonly Lazy<BotNavMeshSet?> DefaultSet = new(() =>
	{
		if (Environment.GetEnvironmentVariable("AION_BOT_NAVMESH") is "0" or "false") return null;
		string? explicitDirectory = Environment.GetEnvironmentVariable("AION_BOT_NAVMESH_DIR");
		if (!string.IsNullOrWhiteSpace(explicitDirectory))
			return System.IO.Directory.Exists(explicitDirectory) ? new BotNavMeshSet(explicitDirectory) : null;
		foreach (string start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
			for (DirectoryInfo? dir = new(start); dir != null; dir = dir.Parent)
			{
				string checkedIn = DefaultDirectory(dir.FullName);
				if (!System.IO.Directory.Exists(checkedIn) ||
					!System.IO.Directory.EnumerateFiles(checkedIn, "*" + BotNavMesh.FileExtension).Any()) continue;
				string local = LocalCacheDirectory(dir.FullName);
				return System.IO.Directory.Exists(local) ? new BotNavMeshSet(checkedIn, local) : new BotNavMeshSet(checkedIn);
			}
		return null;
	});

	/// <summary>The navmeshes found above the running binary, unless disabled with <c>AION_BOT_NAVMESH=0</c>
	/// or redirected with <c>AION_BOT_NAVMESH_DIR</c>.</summary>
	public static BotNavMeshSet? Default => DefaultSet.Value;

	public BotNavMesh? Get(int mapId)
	{
		lock (gate)
		{
			if (loaded.TryGetValue(mapId, out BotNavMesh? mesh)) return mesh;
			string? directory = directories.FirstOrDefault(d => BotNavMesh.Exists(d, mapId));
			mesh = directory == null ? null : BotNavMesh.Load(directory, mapId);
			loaded[mapId] = mesh;
			return mesh;
		}
	}

	public IEnumerable<int> AvailableMapIds() => directories.Where(System.IO.Directory.Exists)
		.SelectMany(d => System.IO.Directory.EnumerateFiles(d, "*" + BotNavMesh.FileExtension))
		.Select(path => Path.GetFileNameWithoutExtension(path))
		.Select(name => int.TryParse(name, out int id) ? id : 0).Where(id => id != 0).Distinct().Order();
}
