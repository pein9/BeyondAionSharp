using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunCapitalAsync(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		var token = timeout.Token;
		await using var session = new SimulationL0Session(fixture, policy, "b01", 55, "Aesimcapital");
		session.BeginStep("s00", "login-create-enter");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token);
		await session.EnterWorldAsync(token);
		await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
		var driver = new SimCapitalDriver(fixture, session, player, "CAPITAL",
			token => SetLevelNineAtAsync(session, player, 210010000, CapitalAscensionScenario.Pernos, token),
			() =>
			{
				Assert.Equal(110010000, player.GetWorldId());
				Assert.Equal(PlayerClass.GLADIATOR, player.GetPlayerClass());
				Assert.Equal(QuestStatus.COMPLETE, player.GetQuestStateList().GetQuestState(1006).GetStatus());
				Assert.Equal(QuestStatus.COMPLETE, player.GetQuestStateList().GetQuestState(1007).GetStatus());
			});
		await CapitalAscensionScenario.RunAsync(driver, token);
		policy.AssertClean();
		QuestCoverageReceipt.SaveFromEnvironment("SIM", scenario.Id, session.Api.World);
	}

	/// <summary>CAPITAL-ASMO (NA-02): the Asmodian Ascension bridge from Munin to the Altgard Fortress bind.</summary>
	private async Task RunCapitalAsmodianAsync(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		var token = timeout.Token;
		NaturalAscensionContract contract = NaturalAscensionContract.LoadDefault();
		await using var session = new SimulationL0Session(fixture, policy, "b01", 56, "Asimascend", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
		float[] start = contract.Steps.First(step => step.NpcId == contract.Start.NpcId).Position;
		var munin = new BotPosition(start[0], start[1], start[2], 0);
		var driver = new SimCapitalDriver(fixture, session, player, "CAPITAL-ASMO",
			token => SetLevelNineAtAsync(session, player, contract.Start.MapId, munin, token),
			() =>
			{
				Assert.Equal(contract.Endpoint.MapId, player.GetWorldId());
				Assert.Equal(PlayerClass.CLERIC, player.GetPlayerClass());
				Assert.True(player.GetCommonData().IsDaeva());
				Assert.True(player.GetLevel() >= contract.Endpoint.MinimumLevel);
				foreach (int quest in contract.Endpoint.CompletedQuestIds)
					Assert.Equal(QuestStatus.COMPLETE, player.GetQuestStateList().GetQuestState(quest).GetStatus());
				Assert.Equal(contract.Bind.MapId, player.GetBindPoint().GetMapId());
				Assert.True(MathF.Abs(player.GetBindPoint().GetX() - contract.Bind.Position[0]) < 5 &&
					MathF.Abs(player.GetBindPoint().GetY() - contract.Bind.Position[1]) < 5);
				Assert.NotNull(player.GetInventory().GetFirstItemByItemId(contract.CeremonyReward.ItemId));
				// Every quest movie was played and answered: the reflex's CM_PLAY_MOVIE_END cleared the cutscene state.
				Assert.Equal(contract.Movies.Order(), session.PacketHistory.Where(packet => packet.PacketType == typeof(SM_PLAY_MOVIE))
					.Select(packet => packet.Get<int>("cutsceneId")).Where(contract.Movies.Contains).Order());
				Assert.False(player.IsInCustomState(CustomPlayerState.WATCHING_CUTSCENE));
			});
		await CapitalAscensionScenario.RunAsmodianAsync(driver, contract, token);
		policy.AssertClean();
		QuestCoverageReceipt.SaveFromEnvironment("SIM", scenario.Id, session.Api.World);
	}

	private async Task SetLevelNineAtAsync(SimulationL0Session session, Player player, int mapId, BotPosition point, CancellationToken token)
	{
		player.GetCommonData().SetLevel(9);
		await TeleportForSetupAsync(session, player, mapId, point.X - 5, point.Y, point.Z, token);
	}

	private sealed class SimCapitalDriver(SimulationWorldFixture world,
		SimulationL0Session session, Player player, string scenarioId, Func<CancellationToken, Task> prepare, Action verify)
		: ICapitalAscensionDriver
	{
		private int step;
		public BotApi Api => session.Api;
		public async Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token)
		{
			session.BeginStep($"s{++step:D2}", action);
			Console.WriteLine($"{scenarioId} s{step:D2}: {action}");
			using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
			deadline.CancelAfter(TimeSpan.FromSeconds(30));
			try { await operation(deadline.Token); }
			catch (Exception exception)
			{
				string packets = string.Join(Environment.NewLine, session.PacketHistory.TakeLast(20).Select(packet => packet.PacketType.Name + " " + System.Text.Json.JsonSerializer.Serialize(packet.Fields)));
				throw new InvalidOperationException($"{scenarioId} step {action} failed at {player.GetWorldId()} ({player.GetX()},{player.GetY()},{player.GetZ()}). Recent packets:{Environment.NewLine}{packets}", exception);
			}
		}
		public Task PrepareAsync(CancellationToken token) => prepare(token);
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => session.SendPacketAsync(packet, token);
		public Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) => session.WaitForPacketAsync(type, token, predicate);
		public Task MoveAsync(BotPosition position, CancellationToken token) => session.MoveToPositionAsync(position, token);
		public Task DelayAsync(TimeSpan duration, CancellationToken token) => session.AdvanceAsync(duration, token).AsTask();
		public Task FlyAsync(BotPosition destination, TimeSpan duration, CancellationToken token) => session.ExecuteMovementAsync(
			CapitalAscensionScenario.CreateQuestFlight(session.CurrentPosition, destination, Api.World.MapId!.Value, duration), token);
		public async Task CompleteTeleportAsync(int mapId, CancellationToken token)
		{
			bool changed = Api.World.MapId != mapId;
			Api.World.BeginWorldReload();
			await WaitAsync(changed ? typeof(SM_PLAYER_SPAWN) : typeof(SM_CHANNEL_INFO), _ => true, token);
			await WaitAsync(typeof(SM_PLAYER_INFO), packet => packet.Get<int>("objectId") == session.CharacterId, token);
			session.AcceptTeleportPosition();
		}
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await SendAsync(GameClientPackets.TimeCheck(unchecked((int)world.Clock.NowMillis)), token);
			await WaitAsync(typeof(SM_TIME_CHECK), _ => true, token);
		}
		public Task VerifyAsync(CancellationToken token)
		{
			verify();
			Assert.Null(player.GetInteractionTask());
			Assert.False(player.IsDead());
			Assert.Empty(Api.Timing.BlockingActivities);
			return Task.CompletedTask;
		}
	}
}
