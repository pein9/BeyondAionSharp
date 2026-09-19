using System.Text.Json;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Dao;
using Aion.GameServer.Model;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunL3Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 114, "Simswitcha");
		await CharacterSwitchScenario.RunAsync(new SimCharacterSwitchDriver(fixture, session), "Simswitcha", "Simswitchb", timeout.Token);
		session.BeginStep("s09", "quit-second-character-and-verify-offline");
		await session.QuitAsync(timeout.Token); await session.VerifyOfflineAsync(timeout.Token);
		policy.AssertClean();
	}

	private sealed class SimCharacterSwitchDriver(SimulationWorldFixture world, SimulationL0Session session) : ICharacterSwitchDriver
	{
		private int step;
		public IReadOnlyList<DecodedBotServerPacket> Packets => session.PacketHistory;
		public int ConnectionGeneration => session.ConnectionGeneration;
		public int? SelfObjectId => session.Api.World.SelfObjectId;
		public async Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token)
		{
			session.BeginStep($"s{++step:D2}", action); Console.WriteLine($"L3 s{step:D2}: {action}");
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(15));
			try { await operation(timeout.Token); }
			catch (Exception exception)
			{
				throw new InvalidOperationException($"L3 step {action}. Recent packets:{Environment.NewLine}" +
					string.Join(Environment.NewLine, Packets.TakeLast(12).Select(p => p.PacketType.Name + " " + JsonSerializer.Serialize(p.Fields))), exception);
			}
		}
		public Task LoginAsync(CancellationToken token) => session.LoginAndAuthenticateAsync(token);
		public async Task<int> CreateAsync(string name, PlayerClass playerClass, CancellationToken token)
		{
			await session.CreateCharacterAsync(name, playerClass, token); return session.CharacterId;
		}
		public Task<DecodedBotServerPacket> ListAsync(CancellationToken token) => session.ReadCharacterListAsync(token);
		public async Task EnterAsync(SwitchCharacter character, CancellationToken token)
		{
			session.SelectCharacter(character.Id, character.Name);
			await session.EnterWorldAsync(token); await session.SynchronizeAsync(token);
			Assert.Equal(0, world.World.GetPlayer(character.Id).GetClientConnection().GetAccount().GetAccessLevel());
		}
		public Task WalkAsync(CancellationToken token) => session.WalkTenMetersAsync(token);
		public Task ReturnToSelectionAsync(CancellationToken token) => session.ReturnToSelectionAsync(false, token);
		public Task VerifyOfflineAsync(CancellationToken token) => session.VerifyOfflineAsync(token);
		public Task WaitForReentryAsync(CancellationToken token) => session.WaitForReentryAsync(token);
		public Task VerifySwitchedAsync(SwitchCharacter first, SwitchCharacter second, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			Assert.False(PlayerDAO.IsOnline(first.Id)); Assert.Null(world.World.GetPlayer(first.Id));
			Assert.True(PlayerDAO.IsOnline(second.Id));
			var active = world.World.GetPlayer(second.Id);
			Assert.NotNull(active); Assert.Equal(second.Name, active.GetName()); Assert.Equal(second.Class, active.GetPlayerClass());
			Assert.Equal(114, active.GetAccount().GetId()); Assert.Equal(0, active.AccessLevel);
			return Task.CompletedTask;
		}
	}
}
