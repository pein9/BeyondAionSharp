using System.Text.Json;
using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.Commons.Database;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Admin;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	[SkippableTheory]
	[InlineData(false, false)] [InlineData(true, false)]
	[InlineData(false, true)] [InlineData(true, true)]
	public void BrokerSaveTasksPersistItemOwnershipIndependentlyOfSharedDirtyState(bool savedBeforeTransfer, bool retainedAtBroker)
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		// Low-level DAO regression, separate from the client-driven E9 scenario. No world players are fabricated.
		const int sellerId = 900000001, buyerId = 900000002;
		var item = ItemFactory.NewItem(BrokerScenario.ItemId, 3);
		item.SetItemLocation(StorageType.BROKER.GetId());
		var kinah = ItemFactory.NewItem(BotWorldModel.KinahItemId, 5000);
		kinah.SetItemLocation(StorageType.CUBE.GetId());
		var registration = new BrokerService.BrokerOpSaveTask(null!, item, null!, sellerId);
		var purchase = new BrokerService.BrokerOpSaveTask(null!, item, kinah, buyerId, retainedAtBroker ? sellerId : null);
		bool itemDeleted = false;
		try
		{
			if (savedBeforeTransfer) registration.Run();
			item.SetItemCount(2);
			if (!retainedAtBroker) item.SetItemLocation(StorageType.CUBE.GetId());
			if (!savedBeforeTransfer) registration.Run();
			purchase.Run();
			AssertStoredOwner(item, retainedAtBroker ? sellerId : buyerId);
			AssertStoredOwner(kinah, buyerId);
			item.SetPersistentState(IPersistable.PersistentState.DELETED);
			purchase.Run(); // A queued ownership write must not resurrect a deleted item.
			itemDeleted = true;
			Assert.DoesNotContain(InventoryDAO.LoadItems(retainedAtBroker ? sellerId : buyerId,
				retainedAtBroker ? StorageType.BROKER : StorageType.CUBE), i => i.GetObjectId() == item.GetObjectId());
		}
		finally
		{
			foreach (var value in itemDeleted ? new[] { kinah } : new[] { item, kinah })
			{
				value.SetPersistentState(IPersistable.PersistentState.DELETED);
				Assert.True(InventoryDAO.Store(value, buyerId));
			}
		}

		static void AssertStoredOwner(Item value, int owner)
		{
			using var connection = DatabaseFactory.GetConnection(); connection.Open();
			using var command = connection.CreateCommand();
			command.CommandText = "SELECT item_owner FROM inventory WHERE item_unique_id=@id";
			command.Parameters.AddWithValue("@id", value.GetObjectId());
			Assert.Equal(owner, Convert.ToInt32(command.ExecuteScalar()));
		}
	}

	private async Task RunE9Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		var token = timeout.Token;
		await using var seller = new SimulationL0Session(fixture, policy, "b01", 84, "Aesimseller");
		await using var buyer = new SimulationL0Session(fixture, policy, "b02", 85, "Aesimbuyer");
		var drivers = new List<SimBrokerDriver>();
		foreach (var session in new[] { seller, buyer })
		{
			session.BeginStep("s00", "login-create-enter-and-prepare-broker");
			await session.LoginAndAuthenticateAsync(token); await session.CreateCharacterAsync(token); await session.EnterWorldAsync(token);
			await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
			var player = fixture.World.GetPlayer(session.CharacterId);
			Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
			Assert.Equal(0, ItemService.AddItem(player, BotWorldModel.KinahItemId, 100_000));
			if (session == seller) Assert.Equal(0, ItemService.AddItem(player, BrokerScenario.ItemId, 10));
			var point = BrokerScenario.Position;
			await TeleportForSetupAsync(session, player, BrokerScenario.MapId, point.X - 5, point.Y, point.Z, token);
			var npc = player.GetPosition().GetWorldMapInstance().GetNpcs(BrokerScenario.NpcId).First(n => n.IsSpawned());
			await session.MoveToPositionAsync(new BotPosition(npc.GetX() - 1, npc.GetY(), npc.GetZ(), 0), token);
			drivers.Add(new SimBrokerDriver(fixture, session, npc.GetObjectId(), player.GetName()));
		}
		await BrokerScenario.RunAsync(drivers[0], drivers[1], token);
		int previousDays = CustomConfig.BROKER_REGISTRATION_EXPIRATION_DAYS;
		try
		{
			// Exercise the actual registration and scheduled expiry paths without replaying eight idle days.
			CustomConfig.BROKER_REGISTRATION_EXPIRATION_DAYS = 0;
			await BrokerScenario.RunExpiryAsync(drivers[0], token);
		}
		finally { CustomConfig.BROKER_REGISTRATION_EXPIRATION_DAYS = previousDays; }
		policy.AssertClean();
	}

	private sealed class SimBrokerDriver(SimulationWorldFixture world, SimulationL0Session session, int npcObjectId, string characterName) : IBrokerScenarioDriver
	{
		private int step;
		public BotApi Api => session.Api;
		public int NpcObjectId => npcObjectId;
		public string CharacterName => characterName;
		public async Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token)
		{
			session.BeginStep($"s{++step:D2}", action);
			Console.WriteLine($"E9 {CharacterName} s{step:D2}: {action}");
			try { await operation(token); }
			catch (Exception exception)
			{
				throw new InvalidOperationException($"Broker {CharacterName} step {action} failed. Recent packets:{Environment.NewLine}" +
					string.Join(Environment.NewLine, session.PacketHistory.TakeLast(20).Select(p => p.PacketType.Name + " " + JsonSerializer.Serialize(p.Fields))), exception);
			}
		}
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => session.SendPacketAsync(packet, token);
		public async Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token)
		{
			await session.DrainServerPacketsAsync(token);
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(5));
			try { return await session.WaitForPacketAsync(type, timeout.Token, predicate); }
			catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException($"Broker step did not receive {type.Name}."); }
		}
		public Task DelayAsync(TimeSpan delay, CancellationToken token) => session.AdvanceAsync(delay, token).AsTask();
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await SendAsync(GameClientPackets.TimeCheck(unchecked((int)world.Clock.NowMillis)), token); await WaitAsync(typeof(SM_TIME_CHECK), _ => true, token);
		}
		public async Task VerifyPersistenceAsync(CancellationToken token)
		{
			session.BeginStep($"s{++step:D2}", "verify-broker-persistence-after-save-tick-and-relogin");
			Console.WriteLine($"E9 {CharacterName} s{step:D2}: verify-broker-persistence-after-save-tick-and-relogin");
			await DelayAsync(TimeSpan.FromSeconds(7), token); // The real broker persistence manager runs every six seconds.
			await SynchronizeAsync(token); VerifyOracle();
			var expected = Api.World.Inventory.OrderBy(p => p.Key).ToArray();
			await session.QuitAsync(token); await session.VerifyOfflineAsync(token); await session.WaitForReentryAsync(token);
			await session.ReloginAndVerifyPersistenceAsync(token); await session.EnterWorldAsync(token); await SynchronizeAsync(token);
			Assert.Equal(expected, Api.World.Inventory.OrderBy(p => p.Key)); VerifyOracle();
			var saved = InventoryDAO.LoadItems(session.CharacterId, StorageType.CUBE).OrderBy(i => i.GetObjectId()).ToArray();
			Assert.Equal(expected.Length, saved.Length);
			for (int i = 0; i < saved.Length; i++)
			{
				Assert.Equal(expected[i].Key, saved[i].GetObjectId()); Assert.Equal(expected[i].Value.ItemId, saved[i].GetItemId());
				Assert.Equal(expected[i].Value.Count, saved[i].GetItemCount());
			}
			using var connection = DatabaseFactory.GetConnection(); connection.Open();
			using var command = connection.CreateCommand();
			command.CommandText = "SELECT (SELECT COUNT(*) FROM broker WHERE seller_id=@id) + (SELECT COUNT(*) FROM inventory WHERE item_owner=@id AND item_location=126)";
			command.Parameters.AddWithValue("@id", session.CharacterId);
			Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar()));
		}
		private void VerifyOracle()
		{
			var player = world.World.GetPlayer(session.CharacterId); Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
			object inventory = typeof(AdminHttpService).Assembly.GetType("Aion.GameServer.Services.Admin.AdminInventory")!.GetMethod("Read")!.Invoke(null, [player])!;
			var response = JsonSerializer.SerializeToElement(new { ok = true, online = true, recipientCharacterId = session.CharacterId, inventory }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
			BotInventoryOracle.Verify(session.CharacterId, Api.World.Inventory.Values.ToArray(), Api.World.Kinah, response, Api.World.CubeExpansion);
		}
	}
}
