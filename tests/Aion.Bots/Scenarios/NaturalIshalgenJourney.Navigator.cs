using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.Movement;
using Aion.Bots.Protocol;
using Aion.Bots.Reflexes;
using Aion.Bots.Timing;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Network.Aion.ServerPackets;

using Require = Aion.Bots.Scenarios.NaturalJourneyRequirements;

namespace Aion.Bots.Scenarios;

public sealed partial class NaturalIshalgenJourney
{
	private sealed class NaturalJourneyNavigator(INaturalJourneySession session,
		BotNavigationGraph graph, BotNavigationGeometry geometry, NaturalJourneyRuntime runtime, bool stopOnDeath) : INaturalNavigationDriver
	{
		public bool AvoidHostileAggro { get; set; }
		public bool InCombat { get; set; }
		public Func<int[], BotPosition, int, CancellationToken, Task>? DefendOnAttackAsync { get; set; }
		public string LastRouteDiagnostic { get; private set; } = "none";
		/// <summary>Travel planner tried first for long non-combat legs (see BotTravelPlanner.PlanJourney).</summary>
		public BotTravelPlanner? Planner { get; init; }
		public List<NaturalNavigationEvent> Events { get; } = [];
		public HashSet<int> UnavailableObjects { get; } = [];
		/// <summary>CP-56a: the monsters a walk-in approach has accepted as its target's pack. While it is set their
		/// circles are no hazard to a route or a segment; it is empty outside that approach.</summary>
		public IReadOnlySet<int> AcceptedPack { get; set; } = new HashSet<int>();
		private BotPosition? lastMovementStart;
		public BotPosition? LastMovementStart => lastMovementStart;
		private bool defending;

		public NaturalNavigationObservation Observe()
		{
			BotWorldModel world = session.Api.World;
			// A walking NPC is placed where its last SM_MOVE was heading (a real client animates it there;
			// a walker that has been quiet for a while has arrived). Monsters that attacked the bot recently
			// keep their reported position: their move target is the bot itself.
			HashSet<int> attackers = session.PacketHistory.Skip(Math.Max(0, session.PacketHistory.Count - 400))
				.Select(packet => runtime.IncomingAttacker(packet, session.CharacterId))
				.OfType<int>().ToHashSet();
			return new(world.MapId, session.CurrentPosition, world.IsDead,
				world.Objects.Values.Where(item => (item.Kind is BotKnownObjectKind.Npc or BotKnownObjectKind.Gatherable) &&
					item.TemplateId != null && !item.IsCorpse &&
					!UnavailableObjects.Contains(item.ObjectId))
					.Select(item => new NaturalNavigationObject(item.ObjectId, item.TemplateId!.Value,
						attackers.Contains(item.ObjectId) ? item.Position : item.SettledPosition, item.PatrolPath)).ToArray());
		}

		public Task<IReadOnlyList<BotPosition>> FindRouteAsync(BotPosition start, BotPosition destination,
			CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			int map = session.Api.World.MapId ?? throw new InvalidDataException("SIM journey map unobserved.");
			BotNavigationHazard[] hazards = ObservedHazards(destination);
			float remaining = Distance(start, destination);
			if (Planner != null && !InCombat && remaining >= BotTravelPlanner.MinimumJourneyDistance)
			{
				BotTravelPlan? plan = Planner.PlanJourney(map, start, destination, session.Api.World.Level, hazards);
				session.TraceDiagnostic(plan == null ? "travel-plan-unavailable" : "travel-plan", new Dictionary<string, object?>
				{
					["start"] = start,
					["destination"] = destination,
					["level"] = session.Api.World.Level,
					["observedHazards"] = hazards.Length,
					["navmeshOutcome"] = BotNavMeshRouter.LastOutcome.ToString(),
					["plan"] = plan == null ? null : BotTravelPlanner.Describe(plan),
					["waypoints"] = plan?.Waypoints.Select(node => new { node.Id, node.Name, node.Kind }).ToArray(),
				});
				if (plan != null)
				{
					LastRouteDiagnostic = $"travel-plan: {BotTravelPlanner.Describe(plan)}, hazards={hazards.Length}, destination={destination}";
					return Task.FromResult(plan.Route);
				}
			}
			if (map == NaturalIshalgenRoads.MapId && !InCombat &&
				session.CurrentStep.StartsWith("ni07-q2006", StringComparison.Ordinal))
			{
				IReadOnlyList<BotPosition> roadRoute = geometry.FindRoadPreferredJourneyPath(
					map, start, destination, NaturalIshalgenRoads.MijouToMauFarms, hazards);
				if (roadRoute.Count != 0)
				{
					LastRouteDiagnostic = $"mapped-road=Mijou-to-Mau-farms, checked={roadRoute.Count}, " +
						$"hazards={hazards.Length}, destination={destination}";
					session.TraceDiagnostic("road-route-selected", new Dictionary<string, object?>
					{
						["corridor"] = "Mijou-to-Mau-farms",
						["start"] = start,
						["destination"] = destination,
						["checkedPoints"] = roadRoute.Count,
						["observedHazards"] = hazards.Length,
					});
					return Task.FromResult(roadRoute);
				}
			}
			BotPosition[] candidates = graph.GetMap(map)?.Waypoints
				.Select(waypoint => waypoint.Position)
				.Where(point => Distance(start, point) is > 15 and <= 120 &&
					Distance(point, destination) < remaining - 15)
				.OrderBy(point => Distance(point, destination)).ThenBy(point => Distance(start, point))
				.Take(24).ToArray() ?? [];
			IReadOnlyList<BotPosition> FindCheckedForwardProgress()
			{
				foreach (BotPosition candidate in candidates)
				{
					if (geometry.TraceEdge(map, start, candidate) is { } edge &&
						BotNavigationGeometry.AvoidsHazards(start, edge, hazards))
						return edge;
				}
				foreach (BotPosition candidate in candidates.Take(8))
				{
					IReadOnlyList<BotPosition> local = geometry.FindLocalPath(map, start, candidate);
					if (local.Count != 0 && BotNavigationGeometry.AvoidsHazards(start, local, hazards))
						return local;
				}
				return [];
			}
			IReadOnlyList<BotPosition> route = graph.FindPath(map, start, destination);
			// A merely closer waypoint can end at a disconnected ledge. Prefer an
			// end-to-end checked route unless observed hostiles make it unsafe.
			if (route.Count == 0 && remaining > 200 && hazards.Length > 0)
				route = FindCheckedForwardProgress();
			if (route.Count == 0 && remaining <= 200) route = geometry.FindLocalPath(map, start, destination);
			if (route.Count == 0)
			{
				session.TraceDiagnostic("global-route-search", new Dictionary<string, object?>
				{
					["start"] = start,
					["destination"] = destination,
					["candidateCount"] = candidates.Length,
					["hazardCount"] = hazards.Length,
				});
				route = geometry.FindJourneyPath(map, start, destination);
			}
			if (route.Count == 0) route = FindCheckedForwardProgress();
			if (route.Count == 0 && Observe().Npcs.Any(npc => Distance(npc.Position, destination) < 0.1f))
				route = geometry.FindInteractionPath(map, start, destination);
			int checkedRoutePoints = route.Count;
			if (route.Count != 0 && !BotNavigationGeometry.AvoidsHazards(start, route, hazards))
			{
				// Even at short range, first try a checked local step around the
				// observed aggro circles before the expensive global avoidance search.
				route = FindCheckedForwardProgress();
				if (route.Count == 0)
				{
					session.TraceDiagnostic("hazard-route-search", new Dictionary<string, object?>
					{
						["start"] = start,
						["destination"] = destination,
						["candidateCount"] = candidates.Length,
						["hazardCount"] = hazards.Length,
					});
					route = geometry.FindJourneyPathAvoiding(map, start, destination, hazards);
				}
			}
			if (route.Count == 0)
				session.TraceDiagnostic("route-failed", new Dictionary<string, object?>
				{
					["start"] = start,
					["destination"] = destination,
					["navmeshOutcome"] = Aion.Bots.Navigation.NavMesh.BotNavMeshRouter.LastOutcome.ToString(),
					["hazardCircles"] = hazards.Select(hazard => new { hazard.Position.X, hazard.Position.Y, hazard.Position.Z, hazard.Radius }).ToArray(),
				});
			LastRouteDiagnostic = $"checked={checkedRoutePoints}, avoided={route.Count}, " +
				$"destination={destination}, distance={Distance(start, destination):F1}, " +
				$"hazards=[{string.Join(';', hazards.Select(hazard =>
					$"{hazard.Position.X:F1}/{hazard.Position.Y:F1}/r{hazard.Radius:F1}"))}]";
			return Task.FromResult(route);
		}

		public bool IsSegmentSafe(IReadOnlyList<BotPosition> segment, int? targetObjectId)
		{
			BotNavigationHazard[] hazards = ObservedHazards(null, targetObjectId);
			return BotNavigationGeometry.AvoidsHazards(session.CurrentPosition, segment, hazards);
		}

		public bool IsSegmentSafe(IReadOnlyList<BotPosition> segment, int? targetObjectId, BotPosition destination)
			=> IsSegmentSafe(segment, targetObjectId, destination, includeDeathSpots: true);

		public bool IsSegmentSafe(IReadOnlyList<BotPosition> segment, int? targetObjectId, BotPosition destination,
			bool includeDeathSpots)
		{
			// Keep the same destination/death-site exemption as FindRouteAsync even while
			// walking to a shipped hint without an observed target. Live hostiles still count.
			BotNavigationHazard[] hazards = ObservedHazards(destination, targetObjectId, includeDeathSpots);
			return BotNavigationGeometry.AvoidsHazards(session.CurrentPosition, segment, hazards);
		}

		public IReadOnlyList<BotPosition> FindCampaignMemoryPreferenceRoute(BotPosition start, BotPosition destination,
			CancellationToken token) => FindRequiredGroundMemoryPreferenceRoute(start, destination, token, "campaign-zone");

		public IReadOnlyList<BotPosition> FindRequiredGroundMemoryPreferenceRoute(BotPosition start, BotPosition destination,
			CancellationToken token, string purpose, int? targetObjectId = null)
		{
			token.ThrowIfCancellationRequested();
			int map = session.Api.World.MapId ?? throw new InvalidDataException("Campaign map unobserved.");
			BotNavigationHazard[] liveHazards = ObservedHazards(destination, targetObjectId, includeDeathSpots: false);
			IReadOnlyList<BotPosition> route = NaturalCampaignZoneRoute.FindMemoryPreferencePath(geometry, map,
				start, destination, liveHazards);
			session.TraceDiagnostic(purpose + "-memory-preference-route", new Dictionary<string, object?>
			{
				["position"] = start, ["goal"] = destination, ["points"] = route.Count,
				["liveHazards"] = liveHazards.Length, ["rememberedDeaths"] = AvoidSpots.Count,
			});
			return route;
		}

		/// <param name="range">CP-18: how near to the target the route must end, from the class profile.</param>
		public IReadOnlyList<BotPosition> FindRangedApproach(BotPosition start, BotPosition target,
			int targetObjectId, float range, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			int map = session.Api.World.MapId ?? throw new InvalidDataException("SIM journey map unobserved.");
			BotNavigationHazard[] otherHazards = ObservedHazards(target, targetObjectId);
			IReadOnlyList<BotPosition> route = geometry.FindRangedApproachPath(
				map, start, target, otherHazards, range);
			LastRouteDiagnostic = $"ranged-approach={route.Count}, target={target}, " +
				$"otherHazards=[{string.Join(';', otherHazards.Select(hazard =>
					$"{hazard.Position.X:F1}/{hazard.Position.Y:F1}/r{hazard.Radius:F1}"))}]";
			session.TraceDiagnostic("combat-ranged-route", new Dictionary<string, object?>
			{
				["start"] = start,
				["target"] = target,
				["targetObjectId"] = targetObjectId,
				["checkedPoints"] = route.Count,
				["otherHazards"] = otherHazards.Length,
				["hazardCircles"] = otherHazards.Select(hazard => new
				{
					hazard.Position.X,
					hazard.Position.Y,
					hazard.Radius,
				}).ToArray(),
			});
			if (route.Count == 0 &&
				Environment.GetEnvironmentVariable("NI07_CAPTURE_BLOCKED_RANGED_ROUTE") == "1" &&
				session.CurrentStep.StartsWith("ni07-q2006", StringComparison.Ordinal))
				throw new InvalidDataException($"NI-07 captured the first blocked Q2006 ranged route: " +
					$"{LastRouteDiagnostic}; trace={session.CombatTracePath}");
			return route;
		}

		public IReadOnlyList<BotPosition> FindCastRecoveryRoute(BotPosition start, BotPosition target,
			int targetObjectId, CancellationToken token)
		{
			int map = session.Api.World.MapId ?? throw new InvalidDataException("Combat map unobserved.");
			BotNavigationHazard[] hazards = ObservedHazards(target, targetObjectId);
			float distance = Distance(start, target);
			// A native obstacle refusal can leave the straight close-in step inside another
			// monster's circle. Try a bounded set of closer, visible ground points around
			// the target, walking around those circles instead of recasting from the same spot.
			// NR-46a: up to six searches from the same start at one instant; they share their edge traces.
			using BotNavigationGeometry.EdgeMemory memory = geometry.RememberEdges();
			foreach (BotPosition goal in geometry.GroundAround(map, target, [3f, 6f, 9f, 12f])
				.Where(point => Distance(point, target) + 2 < distance &&
					geometry.HasLineOfSight(map, point, target) && !hazards.Any(h => h.Contains(point)))
				.OrderBy(point => Distance(start, point)).Take(6))
			{
				token.ThrowIfCancellationRequested();
				IReadOnlyList<BotPosition> route = geometry.FindJourneyPathAvoiding(map, start, goal, hazards);
				if (route.Count > 0 && Distance(start, route[^1]) >= 0.5f &&
					BotNavigationGeometry.AvoidsHazards(start, route, hazards)) return route;
			}
			return [];
		}

		/// <summary>Places to keep clear of on routes: where the Cleric died (the combat's death spots). A swamp mosbear family
		/// senses only 6 m, so its circles are small, but brushing one brings all six: the Leg 4 catch-up died on the same
		/// spot six times walking the same tight route back to Sumarhon.</summary>
		public IReadOnlyList<BotPosition> AvoidSpots { get; set; } = [];
		private const float AvoidSpotRadius = 20f;
		private const float StaleSegmentDistance = 30f;

		private BotNavigationHazard[] ObservedHazards(BotPosition? destination,
			int? targetObjectId = null, bool includeDeathSpots = true)
		{
			if (!AvoidHostileAggro) return [];
			IReadOnlyList<NaturalNavigationObject> observed = Observe().Npcs;
			// Segment checks must use the same destination exemption as route planning.
			// BC-06 otherwise repeatedly rejected the route back to a target beside a death spot.
			BotPosition? objective = destination ?? observed
				.FirstOrDefault(npc => npc.ObjectId == targetObjectId)?.Position;
			return observed
			.Where(npc => npc.ObjectId != targetObjectId && !AcceptedPack.Contains(npc.ObjectId) &&
				(destination == null || Distance(npc.Position, destination.Value) > 0.1f))
			.Select(npc => (npc,
				template: runtime.Data.NpcDataDh.GetNpcTemplate(npc.TemplateId)))
			.Where(entry => runtime.IsAggressive(entry.template) &&
				entry.template.GetAggroRange() > 0)
			.SelectMany(entry => entry.npc.Hazards(entry.template!.GetAggroRange() + 1f, BotPatrolPath.PassingReach))
			// A death spot is avoided unless the destination itself lies there (a target the Cleric died beside).
			.Concat((includeDeathSpots ? AvoidSpots : []).Where(spot => objective == null || Distance(spot, objective.Value) > AvoidSpotRadius)
				.Select(spot => new BotNavigationHazard(spot, AvoidSpotRadius))).ToArray();
		}

		public bool IsSegmentStale(IReadOnlyList<BotPosition> segment) =>
			segment.Count > 0 && Distance(session.CurrentPosition, segment[0]) > StaleSegmentDistance;

		public async Task MoveAsync(IReadOnlyList<BotPosition> segment, CancellationToken token)
		{
			// A segment planned before a revive or a teleport starts where the Cleric no longer is. Walking it would cross
			// straight from the obelisk to the old route (the Leg 4 catch-up walked 270 m that way, back into the mosbears
			// that had just killed it, six times). Refuse it; the caller sees no progress and plans a new route from here.
			if (IsSegmentStale(segment))
			{
				session.TraceDiagnostic("stale-segment-refused", new Dictionary<string, object?>
				{
					["position"] = session.CurrentPosition, ["firstPoint"] = segment[0],
					["distance"] = Distance(session.CurrentPosition, segment[0]),
				});
				return;
			}
			// The regular client closes an NPC window before walking away. This also delivers
			// DIALOG_FINISH to the NPC AI, so it can resume its idle movement and facing.
			if (segment.Count > 0 && session.Api.OpenDialogTargetId is int dialogTarget)
				await session.SendPacketAsync(session.Api.CloseDialog(dialogTarget), token);
			lastMovementStart = session.CurrentPosition;
			await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing)
				.CreateGroundPlan(segment, session.CurrentPosition,
					session.Api.World.MovementSpeed ?? throw new InvalidDataException("SIM movement speed unobserved.")), token);
		}

		public async Task SynchronizeAsync(CancellationToken token)
		{
			int packetStart = session.PacketHistory.Count;
			await session.SynchronizeAsync(token);
			if (stopOnDeath && session.Api.World.IsDead)
			{
				string nearby = string.Join(',', Observe().Npcs.Where(npc =>
					Distance(npc.Position, session.CurrentPosition) < 30)
					.Select(npc => $"{npc.TemplateId}/{npc.ObjectId}@" +
						$"{Distance(npc.Position, session.CurrentPosition):F1}m"));
				session.TraceDiagnostic("stop-on-death", new Dictionary<string, object?>
				{
					["mapId"] = session.Api.World.MapId,
					["level"] = session.Api.World.Level,
					["position"] = session.CurrentPosition,
					["hp"] = session.Api.World.CurrentHp,
					["mp"] = session.Api.World.CurrentMp,
					["nearby"] = nearby,
				});
				throw new InvalidDataException($"NI-07 stop-on-death: {session.CurrentStep}/" +
					$"{session.CurrentAction}, level={session.Api.World.Level}, " +
					$"position={session.CurrentPosition}, nearby={nearby}, " +
					$"combatTrace={session.CombatTracePath}");
			}
			if (defending || DefendOnAttackAsync == null || session.Api.World.IsDead) return;
			int[] attackers = session.PacketHistory.Skip(packetStart)
				.Select(packet => runtime.IncomingAttacker(packet, session.CharacterId, session.Api.World.Summon?.ObjectId))
				.OfType<int>().Distinct().ToArray();
			if (attackers.Length == 0) return;
			defending = true;
			try
			{
				await DefendOnAttackAsync(attackers, lastMovementStart ?? session.CurrentPosition, packetStart, token);
			}
			finally { defending = false; }
		}

		public void Record(NaturalNavigationEvent navigationEvent)
		{
			Events.Add(navigationEvent);
			session.TraceDiagnostic("navigation-decision", new Dictionary<string, object?>
			{
				["action"] = navigationEvent.Action,
				["outcome"] = navigationEvent.Outcome,
				["reason"] = navigationEvent.Reason,
				["mapId"] = navigationEvent.MapId,
				["targetTemplateId"] = navigationEvent.TargetTemplateId,
				["position"] = navigationEvent.Position,
				["destination"] = navigationEvent.Destination,
				["targetObjectId"] = navigationEvent.TargetObjectId,
				["routeSearches"] = navigationEvent.RouteSearches,
				["segments"] = navigationEvent.Segments,
				["plannedRoutePoints"] = navigationEvent.PlannedRoute?.Length,
			});
		}
	}
}
