using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunE6Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
		var token = timeout.Token;
		await using var first = new SimulationL0Session(fixture, policy, "b01", 51, "Aesimtradea");
		await using var second = new SimulationL0Session(fixture, policy, "b02", 52, "Aesimtradeb");
		int offset = 0;
		foreach (var (session, itemId) in new[] { (first, ExchangeScenario.FirstItem), (second, ExchangeScenario.SecondItem) })
		{
			session.BeginStep("s00", "login-create-enter-and-prepare-exchange");
			await session.LoginAndAuthenticateAsync(token);
			await session.CreateCharacterAsync(token);
			await session.EnterWorldAsync(token);
			await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
			Player player = fixture.World.GetPlayer(session.CharacterId);
			Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
			Assert.Equal(0, ItemService.AddItem(player, itemId, ExchangeScenario.SetupCount));
			await TeleportForSetupAsync(session, player, 210010000, 1212 + offset, 1040, 140.756f, token);
			await session.MoveToPositionAsync(new BotPosition(1213 + offset, 1040, 140.756f, 0), token);
			offset++;
		}
		await ExchangeScenario.RunAsync(new SimExchangeDriver(fixture, first, fixture.World.GetPlayer(first.CharacterId)),
			new SimExchangeDriver(fixture, second, fixture.World.GetPlayer(second.CharacterId)), token);
		policy.AssertClean();
	}

	private sealed class SimExchangeDriver(SimulationWorldFixture world, SimulationL0Session session, Player player) : IExchangeScenarioDriver
	{
		private int step;
		public BotApi Api => session.Api;
		public int CharacterId => session.CharacterId;
		public string CharacterName => player.GetName();
		public async Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token)
		{
			session.BeginStep($"s{++step:D2}", action);
			try { await operation(token); }
			catch (Exception exception)
			{
				string packets = string.Join(Environment.NewLine, session.PacketHistory.TakeLast(15).Select(packet =>
					packet.PacketType.Name + " " + System.Text.Json.JsonSerializer.Serialize(packet.Fields)));
				throw new InvalidOperationException($"Exchange step {action}, player {CharacterName}. Recent packets:{Environment.NewLine}{packets}", exception);
			}
		}
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => session.SendPacketAsync(packet, token);
		public async Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token)
		{
			// The other player's action may have queued these packets outside this transport's send path.
			await session.DrainServerPacketsAsync(token);
			return await session.WaitForPacketAsync(type, token, predicate);
		}
		public Task DelayAsync(TimeSpan delay, CancellationToken token) => session.AdvanceAsync(delay, token).AsTask();
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await SendAsync(GameClientPackets.TimeCheck(unchecked((int)world.Clock.NowMillis)), token);
			await WaitAsync(typeof(SM_TIME_CHECK), _ => true, token);
		}
		public Task VerifyReleasedAsync(CancellationToken token)
		{
			Assert.Null(player.GetInteractionTask());
			Assert.False(ExchangeService.GetInstance().IsPlayerInExchange(player));
			var actual = player.GetInventory().GetItemsWithKinah().Concat(player.GetEquipment().GetEquippedItems())
				.GroupBy(item => item.GetItemId()).ToDictionary(group => group.Key, group => group.Sum(item => item.GetItemCount()));
			var observed = Api.World.Inventory.Values.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
			Assert.Equal(actual.OrderBy(pair => pair.Key), observed.OrderBy(pair => pair.Key));
			return Task.CompletedTask;
		}
	}
}
