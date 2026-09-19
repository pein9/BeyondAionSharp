using System.Text.Json;
using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Dao;
using Aion.GameServer.Services.Items;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunL5Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2)); var token = timeout.Token;
		await using var session = new SimulationL0Session(fixture, policy, "b01", 116, "Simpet");
		session.BeginStep("s00", "login-create-enter");
		await session.LoginAndAuthenticateAsync(token); await session.CreateCharacterAsync(token); await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		await PetLifecycleScenario.RunAsync(new SimPetLifecycleDriver(fixture, session), token);
		session.BeginStep("s09", "quit-and-confirm-offline");
		await session.QuitAsync(token); await session.VerifyOfflineAsync(token); policy.AssertClean();
	}

	private sealed class SimPetLifecycleDriver(SimulationWorldFixture world, SimulationL0Session session) : IPetLifecycleDriver
	{
		private int step;
		public BotApi Api => session.Api;
		public int CharacterId => session.CharacterId;
		public async Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token)
		{
			session.BeginStep($"s{++step:D2}", action); Console.WriteLine($"L5 s{step:D2}: {action}");
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(30));
			try { await operation(timeout.Token); }
			catch (Exception exception)
			{
				throw new InvalidOperationException($"L5 step {action}. Recent packets:{Environment.NewLine}" +
					string.Join(Environment.NewLine, session.PacketHistory.TakeLast(12).Select(p => p.PacketType.Name + " " + JsonSerializer.Serialize(p.Fields))), exception);
			}
		}
		public Task PrepareAsync(CancellationToken token)
		{
			token.ThrowIfCancellationRequested(); var player = world.World.GetPlayer(CharacterId);
			Assert.Equal(0, player.AccessLevel);
			Assert.Equal(0, ItemService.AddItem(player, PetLifecycleScenario.EggItem, 1));
			Assert.Equal(0, ItemService.AddItem(player, PetLifecycleScenario.FoodItem, 3));
			return Task.CompletedTask;
		}
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => session.SendPacketAsync(packet, token);
		public Task<DecodedBotServerPacket> WaitAsync(Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) =>
			session.WaitForPacketAsync(typeof(Aion.GameServer.Network.Aion.ServerPackets.SM_PET), token, predicate);
		public Task DelayAsync(TimeSpan duration, CancellationToken token) => session.AdvanceAsync(duration, token).AsTask();
		public Task SynchronizeAsync(CancellationToken token) => session.SynchronizeAsync(token);
		public Task VerifyStateAsync(int petObjectId, bool summoned, int feedProgress, CancellationToken token)
		{
			token.ThrowIfCancellationRequested(); var player = world.World.GetPlayer(CharacterId);
			Assert.Equal(0, player.AccessLevel);
			var owned = Assert.Single(player.GetPetList().GetPets());
			Assert.Equal(petObjectId, owned.GetObjectId()); Assert.Equal(feedProgress, owned.GetFeedProgress().GetDataForPacket());
			if (summoned)
			{
				Assert.NotNull(player.GetPet()); Assert.Equal(petObjectId, player.GetPet().GetObjectId()); Assert.True(player.GetPet().IsSpawned());
			}
			else
			{
				Assert.Null(player.GetPet());
				var persisted = Assert.Single(PlayerPetsDAO.GetPlayerPets(player));
				Assert.Equal(petObjectId, persisted.GetObjectId()); Assert.Equal(PetLifecycleScenario.PetName, persisted.GetName());
				Assert.Equal(feedProgress, persisted.GetFeedProgress().GetDataForPacket());
			}
			return Task.CompletedTask;
		}
		public async Task<IReadOnlyList<DecodedBotServerPacket>> ReloginAsync(CancellationToken token)
		{
			await session.QuitAsync(token); await session.VerifyOfflineAsync(token); await session.WaitForReentryAsync(token);
			int start = session.PacketHistory.Count;
			await session.ReloginAndVerifyPersistenceAsync(token); await session.EnterWorldAsync(token); await session.SynchronizeAsync(token);
			return session.PacketHistory.Skip(start).ToArray();
		}
	}
}
