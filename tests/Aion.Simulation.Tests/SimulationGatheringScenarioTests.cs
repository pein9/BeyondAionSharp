using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Items;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunE2Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
		CancellationToken token = timeout.Token;
		await using var first = new SimulationL0Session(fixture, policy, "b01", 44, "Aesimgathera");
		await using var second = new SimulationL0Session(fixture, policy, "b02", 45, "Aesimgatherb");
		foreach (var session in new[] { first, second })
		{
			session.BeginStep("s00", "login-create-enter");
			await session.LoginAndAuthenticateAsync(token);
			await session.CreateCharacterAsync(token);
			await session.EnterWorldAsync(token);
			await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
		}
		await GatheringNegativeScenario.RunAsync(
			new SimGatheringDriver(this, fixture, first, fixture.World.GetPlayer(first.CharacterId)),
			new SimGatheringDriver(this, fixture, second, fixture.World.GetPlayer(second.CharacterId)), token);
		policy.AssertClean();
	}

	private async Task RunE1Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		CancellationToken token = timeout.Token;
		int originalFailureChance = CraftConfig.MAX_GATHER_FAILURE_CHANCE;
		CraftConfig.MAX_GATHER_FAILURE_CHANCE = 0;
		try
		{
			foreach (var (race, account, name, target) in new[]
			{
				(Race.ELYOS, 42, "Aesimgather", GatheringTarget.YoungAria),
				(Race.ASMODIANS, 43, "Assimgather", GatheringTarget.YoungAzpha),
			})
			{
				await using var session = new SimulationL0Session(fixture, policy, $"b{account - 41:D2}", account, name, race);
				session.BeginStep("s00", "login-create-enter");
				await session.LoginAndAuthenticateAsync(token);
				await session.CreateCharacterAsync(token);
				await session.EnterWorldAsync(token);
				await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
				Player player = fixture.World.GetPlayer(session.CharacterId);
				Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
				await GatheringScenario.RunAsync(new SimGatheringDriver(this, fixture, session, player), target, token);
			}
			policy.AssertClean();
		}
		finally
		{
			CraftConfig.MAX_GATHER_FAILURE_CHANCE = originalFailureChance;
		}
	}

	private sealed class SimGatheringDriver(SimulationFastScenarioTests owner, SimulationWorldFixture world, SimulationL0Session session,
		Player player) : IGatheringNegativeDriver
	{
		private Gatherable node = null!;
		private long depletedAt;
		private int step;
		public BotApi Api => session.Api;
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token)
		{
			session.BeginStep($"s{++step:D2}", action);
			return operation(token);
		}

		public async Task<int> PrepareAsync(GatheringTarget target, CancellationToken token)
		{
			await owner.TeleportForSetupAsync(session, player, target.MapId,
				target.Position.X - 5, target.Position.Y, target.Position.Z, token);
			node = player.GetPosition().GetWorldMapInstance().OfType<Gatherable>().Single(candidate =>
				candidate.IsSpawned() && candidate.GetObjectTemplate().GetTemplateId() == target.TemplateId &&
				Math.Abs(candidate.GetX() - target.Position.X) < 0.01f &&
				Math.Abs(candidate.GetY() - target.Position.Y) < 0.01f);
			Assert.Equal(GatheringTarget.HarvestCount, node.GetObjectTemplate().GetHarvestCount());
			Assert.NotNull(node.GetSpawn());
			Assert.Equal(295, node.GetSpawn()!.GetRespawnTime());
			Assert.Equal(1f, CustomConfig.RESPAWN_TIME_MULTIPLIER);
			Assert.Equal(1, player.GetSkillList().GetSkillLevel(30001));
			await session.MoveToPositionAsync(target.Position with { X = target.Position.X - 1 }, token);
			return node.GetObjectId();
		}

		public Task SendAsync(BotClientPacket packet, CancellationToken token) => session.SendPacketAsync(packet, token);
		public async Task MoveAsync(GatheringTarget target, float distance, bool interrupt, CancellationToken token)
		{
			var end = target.Position with { X = target.Position.X - distance };
			if (!interrupt)
			{
				await session.MoveToPositionAsync(end, token);
				return;
			}
			// Send an actual movement start while gathering: this negative intentionally interrupts the activity.
			await session.SendPacketAsync(GameClientPackets.Move(new MovementPacketData(player.GetX(), player.GetY(), player.GetZ(), 0,
				MovementMask.POSITION | MovementMask.MANUAL | MovementMask.ABSOLUTE,
				X2: end.X, Y2: end.Y, Z2: end.Z)), token);
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(200), token);
			await session.SendPacketAsync(GameClientPackets.Move(new MovementPacketData(end.X, end.Y, end.Z, 0,
				MovementMask.IMMEDIATE)), token);
		}

		public async Task FillCubeAsync(CancellationToken token)
		{
			int free = player.GetInventory().GetFreeSlots();
			Assert.InRange(free, 1, 150);
			Assert.Equal(0, ItemService.AddItem(player, 100000001, free));
			await SynchronizeAsync(token);
			Assert.True(player.GetInventory().IsFull());
		}

		public Task VerifyOccupiedAsync(int gathererId, CancellationToken token)
		{
			Assert.Equal(gathererId, node.GetController().GetGatheringPlayerId());
			Assert.Equal(player.GetObjectId() == gathererId, player.GetInteractionTask() != null);
			return Task.CompletedTask;
		}
		public Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate,
			CancellationToken token) => session.WaitForPacketAsync(type, token, predicate);

		public async Task AwaitHarvestAsync(CancellationToken token)
		{
			Assert.NotNull(player.GetInteractionTask());
			Assert.Equal(player.GetObjectId(), node.GetController().GetGatheringPlayerId());
			long deadline = world.Clock.NowMillis + 60_000;
			while (player.GetInteractionTask() != null)
			{
				long now = world.Clock.NowMillis;
				long next = world.Clock.NextDueMillis ?? throw new InvalidDataException("Gather has no scheduled work.");
				Assert.InRange(next, now, deadline);
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(next - now), token);
			}
			if (!node.IsSpawned())
				depletedAt = world.Clock.NowMillis;
		}

		public async Task SynchronizeAsync(CancellationToken token)
		{
			// CM_PING is reserved for the real 180s heartbeat; rapid pings trigger Java's timer-cheat check.
			await session.SendPacketAsync(GameClientPackets.TimeCheck(unchecked((int)world.Clock.NowMillis)), token);
			await session.WaitForPacketAsync(typeof(SM_TIME_CHECK), token);
		}
		public Task VerifyReleasedAsync(int objectId, bool depleted, CancellationToken token)
		{
			Assert.Equal(node.GetObjectId(), objectId);
			Assert.Null(player.GetInteractionTask());
			Assert.Equal(0, node.GetController().GetGatheringPlayerId());
			Assert.Equal(!depleted, node.IsSpawned());
			Assert.Equal(player.GetInventory().GetKinah(),
				Api.World.Inventory.Values.Where(item => item.ItemId == 182400001).Sum(item => item.Count));
			return Task.CompletedTask;
		}

		public async Task VerifyRespawnAsync(GatheringTarget target, int oldObjectId, CancellationToken token)
		{
			long due = depletedAt + (long)GatheringTarget.RespawnDelay.TotalMilliseconds;
			// Watch the whole absence window, including the final millisecond, not only its endpoint.
			while (world.Clock.NowMillis < due - 1)
			{
				Assert.Empty(MatchingNodes());
				long now = world.Clock.NowMillis;
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Min(1000, due - 1 - now)), token);
				await SynchronizeAsync(token);
			}
			Assert.Empty(MatchingNodes());
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(1), token);
			Gatherable respawn = Assert.Single(MatchingNodes());
			await session.WaitForPacketAsync(typeof(SM_GATHERABLE_INFO), token,
				packet => packet.Get<int>("objectId") == respawn.GetObjectId());
			Assert.Equal(0, respawn.GetController().GetGatheringPlayerId());
			Assert.Null(player.GetInteractionTask());
			Assert.True(Api.World.Objects.ContainsKey(respawn.GetObjectId()));

			IEnumerable<Gatherable> MatchingNodes() => player.GetPosition().GetWorldMapInstance().OfType<Gatherable>()
				.Where(candidate => candidate.IsSpawned() && candidate.GetObjectTemplate().GetTemplateId() == target.TemplateId &&
					Math.Abs(candidate.GetX() - target.Position.X) < 0.01f &&
					Math.Abs(candidate.GetY() - target.Position.Y) < 0.01f);
		}
	}
}
