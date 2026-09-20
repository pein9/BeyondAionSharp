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
		var localSpots = spots.Where(spot => spot.MapId == cohort.MapId && Distance(spot.Position, home) <= 150)
			.Select(spot => spot with { InstanceId = channel + 1 }).ToArray();
		if (localSpots.Length == 0) throw new InvalidDataException("Soak hub has no shipped starter gathering spots.");
		var unreachable = new HashSet<SoakGatheringSpot>();
		SoakGatheringPool.Lease? selected = null;
		IReadOnlyList<BotPosition>? route = null;
		bool approached = false;
		int waits = 0;
		while (selected == null)
		{
			await session.SynchronizeAsync(token);
			foreach (var candidate in world.Objects.Values.Where(value => value.Kind == BotKnownObjectKind.Gatherable && value.TemplateId == target.TemplateId)
				.OrderBy(value => Distance(session.CurrentPosition, value.Position)))
			{
				var spot = new SoakGatheringSpot(cohort.MapId, channel + 1, target.TemplateId, candidate.Position.X, candidate.Position.Y, candidate.Position.Z);
				if (!localSpots.Contains(spot) || unreachable.Contains(spot)) continue;
				selected = pool.TryAcquire(spot, candidate.ObjectId, Stopwatch.GetElapsedTime(epoch));
				if (selected == null) continue;
				route = Route(candidate.Position);
				if (route.Count != 0) break;
				unreachable.Add(spot);
				selected.Dispose(); selected = null;
			}
			if (selected != null) break;
			if (!approached)
			{
				// The nearest spawn can start outside the client's known list. Walk to shipped coordinates,
				// then acquire only an actually observed object, never a fabricated server object id.
				foreach (var spot in localSpots.OrderBy(spot => Distance(session.CurrentPosition, spot.Position)))
				{
					var approach = Route(spot.Position);
					if (approach.Count == 0) { unreachable.Add(spot); continue; }
					await WalkAsync(approach); approached = true; break;
				}
				if (!approached) throw new InvalidDataException("No collision-checked path to any local gathering spot.");
			}
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
			return path.Count != 0 ? path : navigation.Geometry.FindLocalPath(cohort.MapId, session.CurrentPosition, destination);
		}
		Task WalkAsync(IReadOnlyList<BotPosition> path) => session.ExecuteMovementAsync(new BotMover(world, session.Api.Timing)
			.CreateGroundPlan(path, session.CurrentPosition, world.MovementSpeed ?? throw new InvalidDataException("Missing movement speed.")), token);
		static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));
	}
}
