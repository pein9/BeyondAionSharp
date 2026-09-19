using System.Text.Json;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Dao;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunL1Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		var actors = CharacterLifecycleScenario.Cases.Select((value, index) =>
		{
			string name = "Simlife" + (char)('a' + index);
			return new SimCharacterLifecycleDriver(fixture, new SimulationL0Session(fixture, policy, $"b{index + 1:D2}", 101 + index, name, value.Race), value, name, 101 + index);
		}).ToArray();
		try
		{
			Assert.Equal(12, scenario.Bots);
			await CharacterLifecycleScenario.RunAsync(actors, timeout.Token);
			policy.AssertClean();
		}
		finally { foreach (var actor in actors) await actor.DisposeAsync(); }
	}

	private sealed class SimCharacterLifecycleDriver(SimulationWorldFixture world, SimulationL0Session session,
		CharacterLifecycleCase value, string name, int accountId) : ICharacterLifecycleDriver, IAsyncDisposable
	{
		private int step;
		public CharacterLifecycleCase Case => value;
		public int CharacterId => session.CharacterId;
		public string CharacterName => name;
		public DateTimeOffset Now => SystemClock.UtcNow();
		public async Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token)
		{
			session.BeginStep($"s{++step:D2}", action); Console.WriteLine($"L1 {name} s{step:D2}: {action}");
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(15));
			try { await operation(timeout.Token); }
			catch (Exception exception)
			{
				throw new InvalidOperationException($"L1 {value} step {action}. Recent packets:{Environment.NewLine}" +
					string.Join(Environment.NewLine, session.PacketHistory.TakeLast(12).Select(p => p.PacketType.Name + " " + JsonSerializer.Serialize(p.Fields))), exception);
			}
		}
		public Task<DecodedBotServerPacket> LoginAsync(CancellationToken token) => session.LoginCharacterListAsync(token);
		public async Task<DecodedBotServerPacket> CreateAsync(CancellationToken token)
		{
			await session.CreateCharacterAsync(token, Case.Class);
			return session.PacketHistory.Last(p => p.PacketType == typeof(SM_CREATE_CHARACTER));
		}
		public Task<DecodedBotServerPacket> ListAsync(CancellationToken token) => session.ReadCharacterListAsync(token);
		public async Task<DecodedBotServerPacket> DeleteAsync(CancellationToken token)
		{
			await session.DeleteCharacterAsync(token); return await session.WaitForPacketAsync(typeof(SM_DELETE_CHARACTER), token);
		}
		public async Task<DecodedBotServerPacket> RestoreAsync(CancellationToken token)
		{
			await session.RestoreCharacterAsync(token); return await session.WaitForPacketAsync(typeof(SM_RESTORE_CHARACTER), token);
		}
		public Task CloseAsync(CancellationToken token) => session.CloseSelectionAsync(token);
		public Task DelayAsync(TimeSpan delay, CancellationToken token)
		{
			token.ThrowIfCancellationRequested(); world.Clock.Advance(delay); return Task.CompletedTask;
		}
		public Task VerifyStoredAsync(bool exists, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			Assert.False(PlayerDAO.IsOnline(CharacterId));
			var saved = PlayerDAO.LoadPlayerCommonData(CharacterId);
			var ids = PlayerDAO.GetPlayerOidsOnAccount(accountId);
			if (exists)
			{
				Assert.NotNull(saved); Assert.Equal(CharacterId, Assert.Single(ids));
				Assert.Equal(name, saved.GetName()); Assert.Equal(Case.Race, saved.GetRace());
				Assert.Equal(Case.Class, saved.GetPlayerClass()); Assert.Equal(1, saved.GetLevel());
			}
			else { Assert.Null(saved); Assert.Empty(ids); Assert.Null(PlayerDAO.LoadPlayerCommonDataByName(name)); }
			return Task.CompletedTask;
		}
		public ValueTask DisposeAsync() => session.DisposeAsync();
	}
}
