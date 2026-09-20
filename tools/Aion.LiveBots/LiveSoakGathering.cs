using System.Diagnostics;
using Aion.Bots.Movement;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task SoakGatherAsync(L0Actor actor, SoakCohort cohort, SoakGatheringPool pool,
		IReadOnlyList<SoakGatheringSpot> spots, long epoch, SoakEconomyStatistics statistics, CancellationToken token)
	{
		var session = actor.Session;
		var world = session.Api.World;
		var home = SoakStartPoint(cohort);
		int channel = SoakLifePolicy.StarterChannel(cohort);
		var target = cohort.MapId == GatheringTarget.YoungAria.MapId ? GatheringTarget.YoungAria : GatheringTarget.YoungAzpha;
		var navigation = session.Navigation ?? throw new InvalidOperationException("Soak gathering requires checked starter navigation.");
		if (!world.Skills.ContainsKey(30001)) throw new InvalidDataException("Starter subject lost human gathering.");
		// Separate starting exploration choices for the subjects sharing this map/channel.
		int offset = (cohort.Number - 1) / 25 * 2 + (actor.Bot == $"b{cohort.FirstSubject:D2}" ? 0 : 1);
		var search = new SoakGatheringSearch(spots, cohort.MapId, channel, home, offset);
		SoakGatheringPool.Lease? selected = null;
		IReadOnlyList<BotPosition>? route = null;
		int waits = 0;
		while (selected == null)
		{
			await session.SynchronizeAsync(token);
			foreach (var candidate in world.Objects.Values.Where(value => value.Kind == BotKnownObjectKind.Gatherable && value.TemplateId == target.TemplateId)
				.OrderBy(value => Distance(session.CurrentPosition, value.Position)))
			{
				var spot = new SoakGatheringSpot(cohort.MapId, channel + 1, target.TemplateId, candidate.Position.X, candidate.Position.Y, candidate.Position.Z);
				if (!search.Contains(spot)) continue;
				selected = pool.TryAcquire(spot, candidate.ObjectId, Stopwatch.GetElapsedTime(epoch));
				if (selected == null) continue;
				route = Route(candidate.Position);
				if (route.Count != 0) break;
				search.Reject(spot);
				selected.Dispose(); selected = null;
			}
			if (selected != null) break;
			if (search.NextApproach(spot => pool.IsAvailable(spot, Stopwatch.GetElapsedTime(epoch))) is { } approachSpot)
			{
				// Explore beyond the initial known list, including after a nearby node becomes busy.
				// This hint does not reserve anything. The next loop must observe and lease a real object.
				var approach = Route(approachSpot.Position);
				if (approach.Count == 0) search.Reject(approachSpot);
				else
				{
					actor.Trace.WriteAction(actor.LastStep, "soak:gather-explore", new Dictionary<string, object?>
					{ ["x"] = approachSpot.X, ["y"] = approachSpot.Y, ["z"] = approachSpot.Z, ["channel"] = channel });
					await WalkAsync(approach);
				}
			}
			if (search.AllRejected) throw new InvalidDataException("No collision-checked path to any local gathering spot.");
			waits++;
			await Task.Delay(1000, token); // Continue reading on every iteration while nodes are occupied or respawning.
		}
		using (selected)
		{
			await WalkAsync(route!);
			await session.SynchronizeAsync(token);
			var expected = SoakTotals(world);
			long previous = expected.GetValueOrDefault(target.ItemId);
			int skillBefore = world.Skills[30001].Level;
			double probability = SoakProgressProbability.Gather(skillBefore - 1);
			foreach (var packet in session.Api.Gather(selected.ObjectId)) await session.SendPacketAsync(packet, token);
			var start = await session.WaitForPacketAsync(typeof(SM_GATHER_UPDATE), token);
			if (start.Get<byte>("action") != 0) throw new InvalidDataException("Coordinated gather did not start.");
			var result = await session.WaitForPacketAsync(typeof(SM_GATHER_UPDATE), token, packet => packet.Get<byte>("action") >= 5);
			byte outcome = result.Get<byte>("action");
			if (outcome is not (6 or 7) || result.Get<int>("itemId") != target.ItemId)
				throw new InvalidDataException($"Unexpected gather outcome {outcome}.");
			if (outcome == 6)
			{
				SoakAdd(expected, target.ItemId, 1);
				await session.WaitForPacketAsync(previous == 0 ? typeof(SM_INVENTORY_ADD_ITEM) : typeof(SM_INVENTORY_UPDATE_ITEM), token,
					_ => world.Inventory.Values.Where(item => item.ItemId == target.ItemId).Sum(item => item.Count) == previous + 1);
			}
			if (selected.Depletes)
				await session.WaitForPacketAsync(typeof(SM_DELETE), token, packet => packet.Get<int>("objectId") == selected.ObjectId);
			await Task.Delay(250, token);
			await session.SynchronizeAsync(token);
			SoakAssertInventory(world, expected);
			if (world.Objects.ContainsKey(selected.ObjectId) == selected.Depletes || session.Api.Timing.BlockingActivities.Count != 0)
				throw new InvalidDataException("Gathering did not preserve its three-use node/interaction contract.");
			selected.Complete(Stopwatch.GetElapsedTime(epoch));
			actor.Trace.WriteAction(actor.LastStep, "soak:gather-outcome", new Dictionary<string, object?>
			{
				["template"] = target.TemplateId, ["object"] = selected.ObjectId, ["use"] = selected.UseNumber,
				["success"] = outcome == 6, ["skillBefore"] = skillBefore, ["skillDifference"] = skillBefore - 1,
				["expectedProbability"] = probability, ["probabilityModel"] = SoakProgressProbability.Version,
				["depleted"] = selected.Depletes, ["resourceWaits"] = waits, ["channel"] = channel,
			});
			statistics.Observe(actor.Bot, "Gather", probability, outcome == 6);
		}
		// Rendezvous before pair-owned trade/group/duel actions; this is ordinary checked movement.
		var back = Route(home);
		if (back.Count == 0) throw new InvalidDataException("No checked route back to the soak rendezvous.");
		await WalkAsync(back);
		await session.SynchronizeAsync(token);

		IReadOnlyList<BotPosition> Route(BotPosition destination)
		{
			var path = navigation.Graph.FindPath(cohort.MapId, session.CurrentPosition, destination);
			if (path.Count == 0) path = navigation.Geometry.FindLocalPath(cohort.MapId, session.CurrentPosition, destination);
			return path.Count != 0 ? path : navigation.Geometry.FindJourneyPath(cohort.MapId, session.CurrentPosition, destination);
		}
		async Task WalkAsync(IReadOnlyList<BotPosition> path)
		{
			foreach (var segment in path.Chunk(24))
			{
				await session.ExecuteMovementAsync(new BotMover(world, session.Api.Timing)
					.CreateGroundPlan(segment, session.CurrentPosition, world.MovementSpeed ?? throw new InvalidDataException("Missing movement speed.")), token);
				await session.SynchronizeAsync(token);
				if (world.IsDead) throw new InvalidDataException("Gathering subject died while travelling.");
			}
		}
		static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));
	}
}
