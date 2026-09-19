using System.Text;
using System.Text.Json;
using Aion.Bots.Gm;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Commons.Network;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Network.LoginServer.ServerPackets;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunL4Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		await using var subject = new SimulationL0Session(fixture, policy, "b01", 115, "Simpasskey");
		await using var director = new SimulationL0Session(fixture, policy, "gm", 99, "Simdirector");
		bool oldEnabled = SecurityConfig.PASSKEY_ENABLE;
		try
		{
			director.BeginStep("s00", "director-login-create-enter");
			await director.LoginAndAuthenticateAsync(timeout.Token); await director.CreateCharacterAsync(timeout.Token); await director.EnterWorldAsync(timeout.Token);
			await director.SynchronizeAsync(timeout.Token);
			SecurityConfig.PASSKEY_ENABLE = true;
			Assert.Equal(5, SecurityConfig.PASSKEY_WRONG_MAXCOUNT);
			var gm = new SimulationGmFacade(fixture.World.GetPlayer(director.CharacterId));
			await CharacterPasskeyScenario.RunAsync(new SimCharacterPasskeyDriver(fixture, subject, director, gm), timeout.Token);
			subject.BeginStep("s99", "quit-after-reset-recovery");
			await subject.QuitAsync(timeout.Token); await subject.VerifyOfflineAsync(timeout.Token);
			await director.SynchronizeAsync(timeout.Token); await director.QuitAsync(timeout.Token);
			policy.AssertClean();
		}
		finally { SecurityConfig.PASSKEY_ENABLE = oldEnabled; }
	}

	private sealed class SimCharacterPasskeyDriver(SimulationWorldFixture world, SimulationL0Session session,
		SimulationL0Session director, SimulationGmFacade gm) : ICharacterPasskeyDriver
	{
		private int step;
		private int banStart;
		public async Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token)
		{
			session.BeginStep($"s{++step:D2}", action); Console.WriteLine($"L4 s{step:D2}: {action}");
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(15));
			try { await operation(timeout.Token); }
			catch (Exception exception)
			{
				throw new InvalidOperationException($"L4 step {action}. Recent packets:{Environment.NewLine}" +
					string.Join(Environment.NewLine, session.PacketHistory.TakeLast(8).Select(p => p.PacketType.Name + " " + JsonSerializer.Serialize(p.Fields))), exception);
			}
		}
		public async Task PrepareAsync(CancellationToken token)
		{
			await session.LoginAndAuthenticateAsync(token); await session.CreateCharacterAsync(token);
			Assert.False(PlayerPasskeyDAO.ExistCheckPlayerPasskey(115));
			banStart = world.LoginLink.SentPackets.Count;
		}
		public async Task<DecodedBotServerPacket> RequestEntryAsync(CancellationToken token)
		{
			await session.SendPacketAsync(session.Api.EnterWorld(session.CharacterId), token);
			return await session.WaitForPacketAsync(typeof(SM_CHARACTER_SELECT), token);
		}
		public async Task<DecodedBotServerPacket> ExchangeAsync(BotClientPacket packet, CancellationToken token)
		{
			await session.SendPacketAsync(packet, token);
			var response = await session.WaitForPacketAsync(typeof(SM_CHARACTER_SELECT), token);
			if (response.Get<bool>("accepted"))
			{
				int offset = response.Get<short>("messageType") == 2 ? 50 : 2;
				await VerifyStoredAsync(packet.Body[offset..(offset + 48)], token);
			}
			else
			{
				Assert.Null(world.World.GetPlayer(session.CharacterId));
				Assert.False(PlayerDAO.IsOnline(session.CharacterId));
			}
			return response;
		}
		public async Task FinishEntryAsync(CancellationToken token)
		{
			await session.CompleteWorldEntryAsync(token); await session.SynchronizeAsync(token);
			Assert.Equal(0, world.World.GetPlayer(session.CharacterId).AccessLevel);
		}
		public async Task ReloginAsync(CancellationToken token)
		{
			await StepAsync("quit-confirm-offline", async ct => { await session.QuitAsync(ct); await session.VerifyOfflineAsync(ct); }, token);
			await StepAsync("honor-reentry-delay", session.WaitForReentryAsync, token);
			await StepAsync("fresh-authentication", session.ReloginAndVerifyPersistenceAsync, token);
		}
		public Task VerifyStoredAsync(byte[] value, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			Assert.True(PlayerPasskeyDAO.CheckPlayerPasskey(115, Encoding.Unicode.GetString(value)));
			return Task.CompletedTask;
		}
		public Task VerifyNoBanAsync(CancellationToken token)
		{
			token.ThrowIfCancellationRequested(); Assert.Empty(Bans()); return Task.CompletedTask;
		}
		public async Task LockoutAsync(byte[] wrongValue, CancellationToken token)
		{
			CharacterPasskeyScenario.VerifyResult(await ExchangeAsync(GameClientPackets.SubmitCharacterPasskey(wrongValue), token), 3, 5);
			AssertBan(Assert.Single(Bans()), 480, 0);
			Assert.Null(world.World.GetPlayer(session.CharacterId)); Assert.False(PlayerDAO.IsOnline(session.CharacterId));
			// SIM owns the GS only: assert the serialized LS ban request, then close the selection session.
			// LIVE must additionally prove LS kick, refused reauthentication and unban; this is not a fake ban oracle.
			await session.CloseSelectionAsync(token);
		}
		public async Task ResetAsync(string digits, CancellationToken token)
		{
			await gm.ExecuteAsync(new GmCommand("passkeyreset", ["Simpasskey", digits], "removed from block list"), cancellationToken: token);
			var bans = Bans(); Assert.Equal(2, bans.Length); AssertBan(bans[1], -1, director.CharacterId);
		}
		public async Task LoginAfterResetAsync(CancellationToken token) => await session.ReloginAndVerifyPersistenceAsync(token);
		private SM_BAN[] Bans() => world.LoginLink.SentPackets.Skip(banStart).OfType<SM_BAN>().ToArray();
		private static void AssertBan(SM_BAN packet, int duration, int directorId)
		{
			using var body = new PacketBuffer(packet.SerializePayload());
			Assert.Equal(6, body.ReadC()); Assert.Equal(2, body.ReadC()); Assert.Equal(115, body.ReadD());
			Assert.Equal("", body.ReadS()); Assert.Equal(duration, body.ReadD()); Assert.Equal(directorId, body.ReadD());
			Assert.Equal(0, body.Remaining);
		}
	}
}
