using System.Diagnostics;
using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private const int AzphaQuest = 2133;
	private const int Nobekk = 203519;
	private const int AzphaItem = 152000451;

	private static async Task<int> RunNaturalIshalgenGatheringAsync(LiveBotOptions options,
		LiveBotProblemWriter problems, CancellationToken token)
	{
		NaturalIshalgenIdentity identity = NaturalIshalgenIdentityScenario.Identity;
		await using var actor = new L0Actor(options, problems, 1, Race.ASMODIANS, bot: "b01",
			account: identity.AccountName, characterName: identity.CharacterName);
		actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "NI-06" });
		try
		{
			var identityDriver = new LiveNaturalIshalgenIdentityDriver(options, actor, identity);
			NaturalIshalgenIdentityResult subject = await NaturalIshalgenIdentityScenario.EnterAsync(identityDriver, token);
			await WriteNaturalIdentityReceiptAsync(options, identity, subject, token);
			string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
			LiveBotSession session = actor.Session;
			BotWorldModel world = session.Api.World;
			int sequence = 0;
			if (world.MapId != GatheringTarget.YoungAzpha.MapId || world.Level is < 1 or > 9 || world.IsDead)
				throw new InvalidDataException("NI-06 requires a living pre-Ascension Priest in Ishalgen.");
			if (world.CompletedQuests.ContainsKey(AzphaQuest) ||
				world.Quests.TryGetValue(AzphaQuest, out BotQuestState? completed) && completed.Status == 5)
				throw new InvalidDataException("Q2133 was already complete before NI-06; no new natural gathering can be proved.");
			BotNavigationAssets assets = await BotNavigationAssets.LoadAsync(root,
				Path.Combine(options.OutputDirectory, "navigation-cache"), token);
			int channel = world.ChannelInfo?.Index ?? 0;
			session.Navigation = assets.StarterRoute(Race.ASMODIANS, channel + 1);
			if (world.Level < 2)
			{
				var combat = new NaturalPriestLiveDriver(options, actor, root);
				await actor.StepAsync("earn-level-2-by-ordinary-combat", ct => combat.ReachLevelAsync(2, ct), token);
			}
			if (world.Level < 2) throw new InvalidDataException("Q2133 level gate was not earned naturally.");
			var inventory = NaturalIshalgenInventoryPolicy.Load(root, world.Inventory.Values.Select(item => item.ItemId));
			await EnsureCubeAsync();
			int nobekk = await ApproachNobekkAsync();
			if (!world.Quests.TryGetValue(AzphaQuest, out BotQuestState? state) || state.Status < 3)
			{
				await actor.StepAsync("accept-q2133", ct => session.StartQuestAsync(nobekk, AzphaQuest, ct), token);
				await session.WaitForQuestStatusAsync(AzphaQuest, 3, token);
			}
			if (!world.Quests.TryGetValue(AzphaQuest, out state) || state.Status != 3)
				throw new InvalidDataException("Client journal did not show Q2133 active before gathering.");
			if (!world.Skills.TryGetValue(30001, out BotSkill? humanGathering) || humanGathering.Level < 1)
				throw new InvalidDataException("Client did not observe natural human-gathering skill 1.");
			var search = new NaturalIshalgenGatheringPolicy(SoakGatheringPool.StarterSpots(channel + 1));
			long started = Stopwatch.GetTimestamp();
			int attempts = 0, routeFailures = 0;
			while (CountAzpha(world) < 3)
			{
				if (attempts >= 30 || Stopwatch.GetElapsedTime(started) > TimeSpan.FromMinutes(15))
					throw new InvalidDataException($"Q2133 gathering exceeded its bounded budget: {attempts} attempts, " +
						$"{CountAzpha(world)}/3 Azpha, {routeFailures} route failures.");
				await session.SynchronizeAsync(token);
				await EnsureCubeAsync();
				TimeSpan elapsed = Stopwatch.GetElapsedTime(started);
				NaturalGatherChoice choice = search.Decide(session.CurrentPosition, world.Objects.Values
					.Where(item => item.Kind == BotKnownObjectKind.Gatherable && item.TemplateId == GatheringTarget.YoungAzpha.TemplateId)
					.Select(item => new NaturalGatherNode(item.ObjectId, item.Position)).ToArray(), CountAzpha(world), elapsed);
				Publish(choice);
				if (choice.Action == "wait")
				{
					await Task.Delay(choice.Wait ?? TimeSpan.FromSeconds(1), token);
					continue;
				}
				if (choice.Spot is not { } spot) throw new InvalidDataException("Gathering choice has no shipped spot.");
				var navigation = new LiveNaturalIshalgenNavigationDriver(options, actor, ++sequence, AzphaQuest,
					BotKnownObjectKind.Gatherable, choice.ObjectId);
				NaturalNavigationResult arrival = choice.Action == "gather"
					? await NaturalIshalgenNavigator.ApproachObservedObjectAsync(world.MapId!.Value,
						GatheringTarget.YoungAzpha.TemplateId, spot.Position, navigation, "gatherable", token)
					: await NaturalIshalgenNavigator.ExploreAnchorAsync(world.MapId!.Value,
						GatheringTarget.YoungAzpha.TemplateId, spot.Position, navigation, "gatherable", token);
				if (!arrival.Arrived)
				{
					search.RecordUnreachable(spot, Stopwatch.GetElapsedTime(started));
					if (++routeFailures >= 5) throw new InvalidDataException($"Five Azpha route/observation failures; last: {arrival.Reason}");
					continue;
				}
				if (arrival.TargetObjectId is not int objectId)
				{
					search.RecordUnobserved(spot, Stopwatch.GetElapsedTime(started));
					continue;
				}
				if (!world.Objects.TryGetValue(objectId, out BotKnownObject? node) ||
					node.Kind != BotKnownObjectKind.Gatherable || node.TemplateId != GatheringTarget.YoungAzpha.TemplateId)
				{
					search.RecordUnobserved(spot, Stopwatch.GetElapsedTime(started));
					continue;
				}
				if (choice.Action == "explore") continue; // Replan from this newly observed object before interacting.
				long previous = CountAzpha(world);
				attempts++;
				await actor.StepAsync($"gather-azpha-{attempts}", async ct =>
				{
					foreach (var packet in session.Api.Gather(objectId)) await session.SendPacketAsync(packet, ct);
					var first = await session.WaitForPacketAsync(typeof(SM_GATHER_UPDATE), ct,
						packet => packet.Get<byte>("action") is 0 or 8);
					byte outcome = first.Get<byte>("action") == 8 ? (byte)8 :
						(await session.WaitForPacketAsync(typeof(SM_GATHER_UPDATE), ct,
							packet => packet.Get<byte>("action") is 5 or 6 or 7)).Get<byte>("action");
					if (outcome == 6)
					{
						if (CountAzpha(world) == previous)
							await session.WaitForPacketAsync(previous == 0 ? typeof(SM_INVENTORY_ADD_ITEM) : typeof(SM_INVENTORY_UPDATE_ITEM), ct,
								_ => CountAzpha(world) > previous);
						if (CountAzpha(world) <= previous)
							throw new InvalidDataException("Gather success had no client-observed Azpha inventory increase.");
					}
					else if (CountAzpha(world) != previous)
						throw new InvalidDataException("Failed/occupied gathering unexpectedly changed Azpha inventory.");
					search.RecordAttempt(spot, objectId, outcome, Stopwatch.GetElapsedTime(started));
					actor.Trace.WriteAction(actor.LastStep, "natural:gather-outcome", new Dictionary<string, object?>
					{
						["questId"] = AzphaQuest, ["objectId"] = objectId, ["outcome"] = outcome,
						["azphaBefore"] = previous, ["azphaAfter"] = CountAzpha(world), ["attempt"] = attempts,
					});
					Publish(new(outcome == 6 ? "gather-success" : outcome == 7 ? "gather-failed" :
						outcome == 5 ? "gather-interrupted" : "node-occupied",
						$"Gather outcome {outcome}; client inventory now has {CountAzpha(world)}/3 Azpha.",
						spot, objectId, null));
				}, token);
			}
			await EnsureCubeAsync();
			nobekk = await ApproachNobekkAsync();
			await actor.StepAsync("complete-q2133", ct => session.FinishItemQuestAsync(nobekk, AzphaQuest, ct), token);
			if (!world.CompletedQuests.ContainsKey(AzphaQuest) &&
				(!world.Quests.TryGetValue(AzphaQuest, out state) || state.Status != 5))
				throw new InvalidDataException("Q2133 completion was not client-observed.");
			Publish(new("quest-complete", "Q2133 completed after three naturally gathered Azpha items.", null, null, null));
			if (options.DecisionViewSeconds > 0) await Task.Delay(TimeSpan.FromSeconds(options.DecisionViewSeconds), token);
			await actor.StepAsync("quit-without-deleting-character", session.QuitAsync, token);
			actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?>
			{
				["scenario"] = "NI-06", ["characterId"] = subject.CharacterId, ["gatherAttempts"] = attempts,
				["routeFailures"] = routeFailures,
			});
			Console.WriteLine($"LIVE NI-06: retained Priest {identity.CharacterName} completed Q2133 after {attempts} ordinary gather attempts.");
			return 0;

			async Task<int> ApproachNobekkAsync()
			{
				var nav = new LiveNaturalIshalgenNavigationDriver(options, actor, ++sequence, AzphaQuest);
				NaturalNavigationResult approach = await NaturalIshalgenNavigator.ApproachNpcAsync(
					GatheringTarget.YoungAzpha.MapId, Nobekk, ReadIshalgenNpcAnchor(root, Nobekk), nav, token);
				if (!approach.Arrived || approach.TargetObjectId is not int npc)
					throw new InvalidDataException($"Could not naturally approach Nobekk: {approach.Reason}");
				return npc;
			}
			async Task EnsureCubeAsync()
			{
				NaturalInventoryPlan plan = inventory.Decide(world);
				if (!plan.CubePressure) return;
				PublishInventoryDecision(options, actor, plan, "gather-cube-pressure", ++sequence);
				if (plan.Sales.Count != 0) await SellUnneededAtVendorAsync(options, actor, inventory, root, token);
				if (inventory.Decide(world).CubePressure)
					throw new InvalidDataException("Cube pressure persists after sell-only policy; no item grant, deletion, or purchase allowed.");
			}
			void Publish(NaturalGatherChoice choice)
			{
				var decision = new NaturalDecision(++sequence, choice.Action, AzphaQuest,
					choice.Action is "complete" or "quest-complete" ? "completed" : "planned", choice.Reason,
					[
						new("source", "pass", "Shipped spot is a route hint; object id must be client-observed."),
						new("inventory", "pass", $"Azpha {CountAzpha(world)}/3; free cube slots {inventory.Decide(world).FreeSlots}."),
						new("recovery", "pass", $"Attempts {attempts}/30; route failures {routeFailures}/5; " +
							$"next wait {(choice.Wait is { } wait ? $"{wait.TotalSeconds:F1}s" : "none")}."),
					], []);
				actor.Trace.WriteAction(actor.LastStep, "natural:gather-decision", new Dictionary<string, object?>
				{
					["decision"] = decision, ["spot"] = choice.Spot, ["objectId"] = choice.ObjectId,
				});
				options.Dashboard.PublishDecision(actor.Bot, decision);
			}
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception)
		{
			Console.Error.WriteLine($"NI-06 failed: {exception}");
			return 1;
		}
	}

	private static long CountAzpha(BotWorldModel world) => world.Inventory.Values
		.Where(item => item.ItemId == AzphaItem).Sum(item => item.Count);
}
