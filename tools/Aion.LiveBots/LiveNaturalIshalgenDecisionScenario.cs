using Aion.Bots.Scenarios;
using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task<int> RunNaturalIshalgenDecisionAsync(
		LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token, bool navigate = false)
	{
		string scenario = navigate ? "NI-03" : "NI-02";
		BotNavigationAssets? assets = null;
		if (navigate)
		{
			string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
			assets = await BotNavigationAssets.LoadAsync(repoRoot, Path.Combine(options.OutputDirectory, "navigation-cache"), token);
		}
		NaturalIshalgenIdentity identity = NaturalIshalgenIdentityScenario.Identity;
		await using var actor = new L0Actor(options, problems, 1, Race.ASMODIANS, bot: "b01",
			account: identity.AccountName, characterName: identity.CharacterName);
		actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = scenario });
		try
		{
			var identityDriver = new LiveNaturalIshalgenIdentityDriver(options, actor, identity);
			NaturalIshalgenIdentityResult subject = await NaturalIshalgenIdentityScenario.EnterAsync(identityDriver, token);
			await WriteNaturalIdentityReceiptAsync(options, identity, subject, token);
			NaturalIshalgenContract contract = NaturalIshalgenContract.LoadDefault();
			var decisionDriver = new LiveNaturalIshalgenDecisionDriver(options, actor);
			NaturalDecision decision = await NaturalIshalgenDecisionLoop.RunAsync(contract, decisionDriver, token: token);
			if (decision.Outcome == "blocked")
				throw new InvalidDataException($"{scenario} decision loop blocked: {decision.Reason}");
			NaturalNavigationResult? navigationResult = null;
			if (navigate)
			{
				if (decision.SelectedAction != "find-quest-starter" || decision.SelectedQuestId != 2101)
					throw new InvalidDataException("NI-03 requires the untouched Q2101 starter as its bounded navigation proof.");
				int channel = actor.Session.Api.World.ChannelInfo?.Index ?? 0;
				var navigation = assets!.StarterRoute(Race.ASMODIANS, channel + 1);
				actor.Session.Navigation = navigation;
				actor.Session.TravelPlanner = assets.TravelPlanner(contract.MapId, navigation.Geometry);
				BotWaypoint anchor = navigation.Graph.GetMap(contract.MapId)?.Waypoints
					.Where(waypoint => waypoint.TemplateId == 203500 && waypoint.Sources.HasFlag(BotWaypointSource.QuestNpc))
					.OrderBy(waypoint => waypoint.Id).FirstOrDefault()
					?? throw new InvalidDataException("Shipped Ishalgen route has no Asak quest waypoint.");
				var navigationDriver = new LiveNaturalIshalgenNavigationDriver(options, actor, decision.Sequence);
				navigationResult = await NaturalIshalgenNavigator.ApproachNpcAsync(contract.MapId, 203500,
					anchor.Position, navigationDriver, token);
				if (!navigationResult.Arrived)
					throw new InvalidDataException($"NI-03 navigation failed: {navigationResult.Reason}");
			}
			if (options.DecisionViewSeconds > 0)
			{
				actor.Trace.WriteAction(actor.LastStep, "natural:dashboard-view-window", new Dictionary<string, object?>
				{
					["seconds"] = options.DecisionViewSeconds,
				});
				await Task.Delay(TimeSpan.FromSeconds(options.DecisionViewSeconds), token);
			}
			await actor.StepAsync("quit-without-deleting-character", actor.Session.QuitAsync, token);
			actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?>
			{
				["scenario"] = scenario, ["decision"] = decision, ["navigation"] = navigationResult,
			});
			Console.WriteLine($"LIVE {scenario}: retained Priest {identity.CharacterName} ({subject.CharacterId}); " +
				(navigationResult == null
					? $"selected {decision.SelectedAction} for Q{decision.SelectedQuestId?.ToString() ?? "none"} ({decision.Outcome})."
					: $"approached client-observed Asak for Q2101 in {navigationResult.Segments} paced segments; no quest interaction."));
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception)
		{
			Console.Error.WriteLine($"{scenario} failed: {exception}");
			return 1;
		}
	}

	private sealed class LiveNaturalIshalgenNavigationDriver(LiveBotOptions options, L0Actor actor, int decisionSequence,
		int? selectedQuestId = 2101, BotKnownObjectKind objectKind = BotKnownObjectKind.Npc,
		int? targetObjectId = null)
		: INaturalNavigationDriver
	{
		public NaturalNavigationObservation Observe()
		{
			BotWorldModel world = actor.Session.Api.World;
			BotPosition? position = world.MapId == null ? null : actor.Session.CurrentPosition;
			return new(world.MapId, position, world.IsDead, world.Objects.Values
				.Where(item => item.Kind == objectKind && !item.IsCorpse && (targetObjectId == null || item.ObjectId == targetObjectId))
				.Select(item => new NaturalNavigationObject(item.ObjectId, item.TemplateId ?? 0, item.Position)).ToArray());
		}

		public Task<IReadOnlyList<BotPosition>> FindRouteAsync(BotPosition start, BotPosition destination, CancellationToken token) =>
			LiveNavigationWorkQueue.Shared.RunAsync(() =>
			{
				var navigation = actor.Session.Navigation ?? throw new InvalidOperationException("NI-03 navigation assets are missing.");
				int map = actor.Session.Api.World.MapId ?? throw new InvalidDataException("NI-03 map is unobserved.");
				// Long legs: the level-aware travel planner (roads, hubs, static danger) first.
				if (actor.Session.TravelPlanner?.PlanJourney(map, start, destination, actor.Session.Api.World.Level, []) is { } plan)
				{
					actor.Trace.WriteAction(actor.LastStep, "natural:travel-plan", new Dictionary<string, object?>
					{
						["start"] = start,
						["destination"] = destination,
						["plan"] = Aion.Bots.Navigation.NavMesh.BotTravelPlanner.Describe(plan),
					});
					return plan.Route;
				}
				var route = navigation.Graph.FindPath(map, start, destination);
				if (route.Count == 0) route = navigation.Geometry.FindLocalPath(map, start, destination);
				if (route.Count == 0) route = navigation.Geometry.FindJourneyPath(map, start, destination);
				if (route.Count != 0) return route;
				// A sparse spawn graph may have no end-to-end chain even when nearby road/spawn
				// anchors are reachable. Advance one checked hop, then reobserve and replan.
				float remaining = Distance(start, destination);
				BotPosition[] candidates = navigation.Graph.GetMap(map)?.Waypoints
					.Select(waypoint => waypoint.Position)
					.Where(point => Distance(start, point) is > 15 and <= 120 &&
						Distance(point, destination) < remaining - 15)
					.OrderBy(point => Distance(point, destination)).ThenBy(point => Distance(start, point))
					.Take(24).ToArray() ?? [];
				foreach (BotPosition candidate in candidates)
				{
					var edge = navigation.Geometry.TraceEdge(map, start, candidate);
					if (edge != null) return edge;
				}
				foreach (BotPosition candidate in candidates.Take(8))
				{
					route = navigation.Geometry.FindLocalPath(map, start, candidate);
					if (route.Count != 0) return route;
				}
				return route;
			}, token);

		private static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt(
			MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));

		public Task MoveAsync(IReadOnlyList<BotPosition> segment, CancellationToken token) =>
			actor.StepAsync("natural-walk-segment", ct => actor.Session.ExecuteMovementAsync(
				new BotMover(actor.Session.Api.World, actor.Session.Api.Timing).CreateGroundPlan(segment,
					actor.Session.CurrentPosition, actor.Session.Api.World.MovementSpeed
						?? throw new InvalidDataException("Client movement speed is unobserved.")), ct), token);

		public Task SynchronizeAsync(CancellationToken token) =>
			actor.StepAsync("synchronize-navigation", actor.Session.SynchronizeAsync, token);

		public void Record(NaturalNavigationEvent navigationEvent)
		{
			actor.Trace.WriteAction(actor.LastStep, "natural:navigation", new Dictionary<string, object?>
			{
				["navigation"] = navigationEvent,
			});
			var decision = new NaturalDecision(decisionSequence + navigationEvent.Sequence,
				navigationEvent.Action, selectedQuestId, navigationEvent.Outcome, navigationEvent.Reason,
				[
					new("position-source", "pass", "Client-estimated self position; no server self-move echo."),
					new("navigation-budget", navigationEvent.Outcome == "blocked" ? "blocked" : "pass",
						$"Route searches {navigationEvent.RouteSearches}, segments {navigationEvent.Segments}, target object {navigationEvent.TargetObjectId?.ToString() ?? "unobserved"}."),
				], []);
			options.Dashboard.PublishDecision(actor.Bot, decision);
		}
	}

	private sealed class LiveNaturalIshalgenDecisionDriver(LiveBotOptions options, L0Actor actor)
		: INaturalIshalgenDecisionDriver
	{
		public NaturalIshalgenObservation Observe(bool fresh)
		{
			BotWorldModel world = actor.Session.Api.World;
			return new NaturalIshalgenObservation(fresh && world.LoginStateObserved,
				world.QuestJournalObserved, world.CompletedJournalObserved,
				world.MapId, world.Level, world.IsDead,
				new Dictionary<int, BotQuestState>(world.Quests), world.CompletedQuestIds.ToHashSet(),
				world.MapId == null ? null : actor.Session.CurrentPosition, world.Objects.Values.ToArray());
		}

		public Task RefreshAsync(CancellationToken token) =>
			actor.StepAsync("synchronize-client-world", actor.Session.SynchronizeAsync, token);

		public void Record(NaturalDecision decision)
		{
			actor.Trace.WriteAction(actor.LastStep, "natural:decision", new Dictionary<string, object?>
			{
				["decision"] = decision,
			});
			options.Dashboard.PublishDecision(actor.Bot, decision);
		}
	}
}
