using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Items;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunE7Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
		var token = timeout.Token;
		await using var first = new SimulationL0Session(fixture, policy, "b01", 53, "Aesimmaila");
		await using var second = new SimulationL0Session(fixture, policy, "b02", 54, "Aesimmailb");
		foreach (var session in new[] { first, second })
		{
			session.BeginStep("s00", "login-create-enter-for-mail");
			await session.LoginAndAuthenticateAsync(token);
			await session.CreateCharacterAsync(token);
			await session.EnterWorldAsync(token);
			await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
			Assert.Equal(0, fixture.World.GetPlayer(session.CharacterId).GetClientConnection().GetAccount().GetAccessLevel());
		}
		Assert.Equal(0, ItemService.AddItem(fixture.World.GetPlayer(first.CharacterId), MailScenario.ItemId, MailScenario.SetupCount));
		await MailScenario.RunAsync(new SimMailDriver(fixture, first, fixture.World.GetPlayer(first.CharacterId)),
			new SimMailDriver(fixture, second, fixture.World.GetPlayer(second.CharacterId)), token);
		policy.AssertClean();
	}

	private sealed class SimMailDriver(SimulationWorldFixture world, SimulationL0Session session, Player player) : IMailScenarioDriver
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
				throw new InvalidOperationException($"Mail step {action}, player {CharacterName}. Recent packets:{Environment.NewLine}{packets}", exception);
			}
		}
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => session.SendPacketAsync(packet, token);
		public async Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token)
		{
			await session.DrainServerPacketsAsync(token);
			return await session.WaitForPacketAsync(type, token, predicate);
		}
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await SendAsync(GameClientPackets.TimeCheck(unchecked((int)world.Clock.NowMillis)), token);
			await WaitAsync(typeof(SM_TIME_CHECK), _ => true, token);
		}
		public Task VerifyMailboxAsync(int letterId, long itemCount, long kinah, bool deleted, CancellationToken token)
		{
			// Send stores attachments before the FK-bearing letter; claiming kinah persists the emptied letter.
			foreach (var mailbox in new[] { player.GetMailbox(), MailDAO.LoadPlayerMailbox(player) })
			{
				var letter = mailbox.GetLetterFromMailbox(letterId);
				if (deleted) Assert.Null(letter);
				else
				{
					Assert.NotNull(letter);
					Assert.Equal(itemCount, letter.GetAttachedItem()?.GetItemCount() ?? 0);
					Assert.Equal(kinah, letter.GetAttachedKinah());
					if (itemCount != 0) Assert.Equal(MailScenario.ItemId, letter.GetAttachedItem().GetItemId());
				}
			}
			return Task.CompletedTask;
		}
		public Task VerifyInventoryAsync(CancellationToken token)
		{
			Assert.Null(player.GetInteractionTask());
			var actual = player.GetInventory().GetItemsWithKinah().Concat(player.GetEquipment().GetEquippedItems())
				.GroupBy(item => item.GetItemId()).ToDictionary(group => group.Key, group => group.Sum(item => item.GetItemCount()));
			var observed = Api.World.Inventory.Values.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
			Assert.Equal(actual.OrderBy(pair => pair.Key), observed.OrderBy(pair => pair.Key));
			return Task.CompletedTask;
		}
	}
}
