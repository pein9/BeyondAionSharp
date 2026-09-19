using System.Text.Json;
using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Dao;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Items;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunL2Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2)); var token = timeout.Token;
		await using var session = new SimulationL0Session(fixture, policy, "b01", 113, "Aesimsettings");
		session.BeginStep("s00", "login-create-enter");
		await session.LoginAndAuthenticateAsync(token); await session.CreateCharacterAsync(token); await session.EnterWorldAsync(token);
		await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
		await CharacterSettingsScenario.RunAsync(new SimCharacterSettingsDriver(this, fixture, session), token);
		policy.AssertClean();
	}

	private sealed class SimCharacterSettingsDriver(SimulationFastScenarioTests owner, SimulationWorldFixture world, SimulationL0Session session) : ICharacterSettingsDriver
	{
		private int step;
		public BotApi Api => session.Api;
		public int CharacterId => session.CharacterId;
		public string CharacterName => "Aesimsettings";
		public async Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token)
		{
			session.BeginStep($"s{++step:D2}", action); Console.WriteLine($"L2 s{step:D2}: {action}");
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(15));
			try { await operation(timeout.Token); }
			catch (Exception exception)
			{
				throw new InvalidOperationException($"L2 step {action}. Recent packets:{Environment.NewLine}" +
					string.Join(Environment.NewLine, session.PacketHistory.TakeLast(12).Select(p => p.PacketType.Name + " " + JsonSerializer.Serialize(p.Fields))), exception);
			}
		}
		public async Task<int> PrepareAsync(CancellationToken token)
		{
			var player = world.World.GetPlayer(CharacterId);
			Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
			Assert.Equal(0, ItemService.AddItem(player, CharacterSettingsScenario.SurgeryTicket, 1));
			Assert.True(player.GetTitleList().AddTitle(1, false, 0)); Assert.True(player.GetTitleList().AddTitle(2, false, 0));
			var point = CharacterSettingsScenario.SurgeonPosition;
			await owner.TeleportForSetupAsync(session, player, CharacterSettingsScenario.SurgeonMap, point.X - 5, point.Y, point.Z, token);
			var npc = Assert.Single(player.GetPosition().GetWorldMapInstance().GetNpcs(CharacterSettingsScenario.SurgeonNpc), n => n.IsSpawned());
			await session.MoveToPositionAsync(point with { X = point.X - 1 }, token);
			return npc.GetObjectId();
		}
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => session.SendPacketAsync(packet, token);
		public Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) => session.WaitForPacketAsync(type, token, predicate);
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await SendAsync(GameClientPackets.TimeCheck(unchecked((int)world.Clock.NowMillis)), token);
			await WaitAsync(typeof(SM_TIME_CHECK), _ => true, token);
		}
		public Task ReturnToEditScreenAsync(CancellationToken token) => session.ReturnToSelectionAsync(true, token);
		public Task EditAndEnterAsync(CharacterCreationData appearance, CancellationToken token) => session.EditAndEnterAsync(appearance, token);
		public async Task<IReadOnlyList<DecodedBotServerPacket>> ReloginAsync(CancellationToken token)
		{
			await StepAsync("quit-and-confirm-offline", async ct => { await session.QuitAsync(ct); await session.VerifyOfflineAsync(ct); }, token);
			await StepAsync("honor-reentry-delay", session.WaitForReentryAsync, token);
			int start = session.PacketHistory.Count;
			await StepAsync("fresh-login-and-enter-world", async ct =>
			{
				await session.ReloginAndVerifyPersistenceAsync(ct); await session.EnterWorldAsync(ct); await SynchronizeAsync(ct);
			}, token);
			return session.PacketHistory.Skip(start).ToArray();
		}
		public Task VerifyStoredAsync(CharacterSettingsExpected expected, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			var player = world.World.GetPlayer(CharacterId);
			Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
			Assert.Equal(0L, player.GetInventory().GetItemCountByItemId(CharacterSettingsScenario.SurgeryTicket));
			var common = PlayerDAO.LoadPlayerCommonData(CharacterId);
			Assert.Equal(expected.DisplayTitle, common.GetTitleId()); Assert.Equal(expected.BonusTitle, common.GetBonusTitleId());
			var appearance = PlayerAppearanceDAO.Load(CharacterId);
			Assert.Equal(expected.Appearance.Voice, appearance.GetVoice()); Assert.Equal(expected.Appearance.Height, appearance.GetHeight());
			Assert.Equal(expected.Appearance.SkinRgb, appearance.GetSkinRGB()); Assert.Equal(expected.Appearance.HairRgb, appearance.GetHairRGB());
			Assert.Equal(expected.Appearance.EyeRgb, appearance.GetEyeRGB()); Assert.Equal(expected.Appearance.LipRgb, appearance.GetLipRGB());
			var macros = PlayerMacrosDAO.LoadMacros(CharacterId).GetAll().OrderBy(m => m.Id).Select(m => new BotMacro((byte)m.Id, m.Xml));
			Assert.Equal(expected.Macros.OrderBy(p => p.Key).Select(p => new BotMacro(p.Key, p.Value)), macros);
			var settings = PlayerSettingsDAO.LoadSettings(CharacterId);
			Assert.Equal(expected.UiSettings[0], settings.GetUiSettings()); Assert.Equal(expected.UiSettings[1], settings.GetShortcuts());
			Assert.Equal(expected.UiSettings[2], settings.GetHouseBuddies());
			return Task.CompletedTask;
		}
	}
}
