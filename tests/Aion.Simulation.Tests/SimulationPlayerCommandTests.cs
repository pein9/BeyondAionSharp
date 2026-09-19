using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Commons.Network;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Network.LoginServer.ServerPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunL8CommandsAsync(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2)); var token = timeout.Token;
		Assert.Equal(new DateTimeOffset(2026, 12, 16, 12, 0, 0, TimeSpan.Zero), fixture.Epoch);
		Assert.True(EventsConfig.ENABLE_ADVENT_CALENDAR);
		await using var session = new SimulationL0Session(fixture, policy, "b01", 119, "Simcommands");
		session.BeginStep("s00", "login-create-enter");
		await session.LoginAndAuthenticateAsync(token); await session.CreateCharacterAsync(token);
		await session.EnterWorldAsync(token); await session.SynchronizeAsync(token);
		var commands = ChatProcessor.GetInstance().GetCommandList().OfType<PlayerCommand>().ToArray();
		Assert.Equal(16, commands.Length);
		Assert.Equal(PlayerCommandScenario.Aliases.Order(), commands.Select(c => c.GetAlias()).Order());
		Assert.All(commands, c => Assert.True(c.ValidateAccess(fixture.World.GetPlayer(session.CharacterId)), c.GetAlias()));
		await PlayerCommandScenario.RunAsync(new SimPlayerCommandDriver(fixture, session), token);
		session.BeginStep("s99", "quit-and-confirm-offline");
		await session.QuitAsync(token); await session.VerifyOfflineAsync(token); policy.AssertClean();
	}

	private sealed class SimPlayerCommandDriver(SimulationWorldFixture world, SimulationL0Session session) : IPlayerCommandScenarioDriver
	{
		private int step;
		private readonly List<byte[]> locks = [];
		private Dictionary<int, long> originalEquipment = [];
		public BotApi Api => session.Api;
		public IReadOnlyList<DecodedBotServerPacket> History => session.PacketHistory;
		public async Task PrepareAsync(CancellationToken token)
		{
			var player = world.World.GetPlayer(session.CharacterId);
			Assert.Equal(0, player.AccessLevel); Assert.False(player.IsStaff());
			Assert.Equal(10_000, CustomConfig.FACTION_USE_PRICE); Assert.True(CustomConfig.FACTION_CMD_CHANNEL);
			Assert.False(CustomConfig.FACTION_CHAT_CHANNEL);
			Assert.False(player.GetCommonData().GetNoExp());
			Assert.Null(player.GetQuestStateList().GetQuestState(1101));
			Assert.True(AdventDAO.CanReceiveReward(player, new DateOnly(2026, 12, 16)));
			originalEquipment = player.GetEquipment().GetEquippedItems().ToDictionary(i => i.GetObjectId(), i => i.GetEquipmentSlot());
			foreach (var grant in PlayerCommandScenario.Grants) Assert.Equal(0, ItemService.AddItem(player, grant.Id, grant.Count));
			await session.SynchronizeAsync(token);
		}
		public async Task StepAsync(string name, Func<CancellationToken, Task> action, CancellationToken token)
		{
			session.BeginStep($"s{++step:D2}", name); Console.WriteLine($"L8C s{step:D2}: {name}");
			try { await action(token); }
			catch (Exception error)
			{
				throw new InvalidOperationException($"L8C {name} failed. Recent packets:\n" + string.Join('\n', History.TakeLast(15)
					.Select(p => p.PacketType.Name + " " + System.Text.Json.JsonSerializer.Serialize(p.Fields))), error);
			}
		}
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => session.SendPacketAsync(packet, token);
		public Task SynchronizeAsync(CancellationToken token) => session.SynchronizeAsync(token);
		public Task DelayAsync(TimeSpan delay, CancellationToken token) => session.AdvanceAsync(delay, token).AsTask();
		public Task VerifyStateAsync(string action, IReadOnlyDictionary<int, long> inventory, CancellationToken token)
		{
			token.ThrowIfCancellationRequested(); var player = world.World.GetPlayer(session.CharacterId);
			Assert.Equal(0, player.AccessLevel); Assert.False(player.IsStaff());
			var actual = player.GetInventory().GetItemsWithKinah().Concat(player.GetEquipment().GetEquippedItems())
				.GroupBy(i => i.GetItemId()).ToDictionary(g => g.Key, g => g.Sum(i => i.GetItemCount()));
			Assert.Equal(inventory.OrderBy(p => p.Key), actual.OrderBy(p => p.Key));
			Assert.Equal(originalEquipment.OrderBy(p => p.Key), player.GetEquipment().GetEquippedItems()
				.ToDictionary(i => i.GetObjectId(), i => i.GetEquipmentSlot()).OrderBy(p => p.Key));
			if (action.StartsWith("noexp-", StringComparison.Ordinal)) Assert.Equal(action == "noexp-on", player.GetCommonData().GetNoExp());
			if (action.StartsWith("nomorph-", StringComparison.Ordinal)) Assert.Equal(action == "nomorph-on" ? player.GetObjectTemplate().GetTemplateId() : 0, player.GetTransformModel().GetEventModelId());
			if (action.StartsWith("lock-", StringComparison.Ordinal))
			{
				string? expected = action == "lock-on" ? "SIM-B01" : null;
				Assert.Equal(expected, player.GetAccount().GetAllowedHddSerial());
				var requests = world.LoginLink.SentPackets.OfType<SM_CHANGE_ALLOWED_HDD_SERIAL>().ToArray();
				Assert.Equal(locks.Count + 1, requests.Length);
				byte[] bytes = requests.Last().SerializePayload(); locks.Add(bytes);
				using var body = new PacketBuffer(bytes);
				Assert.Equal(11, body.ReadC()); Assert.Equal(119, body.ReadD()); Assert.Equal(expected ?? "", body.ReadS()); Assert.Equal(0, body.Remaining);
			}
			if (action is "advent-get" or "advent-duplicate" or "advent-persisted" or "relogin")
			{
				Assert.False(AdventDAO.CanReceiveReward(player, new DateOnly(2026, 12, 16)));
				Assert.True(AdventDAO.CanReceiveReward(player, new DateOnly(2026, 12, 17)));
			}
			if (action == "preview-restored")
			{
				var receipt = History.Last(p => p.PacketType == typeof(SM_UPDATE_PLAYER_APPEARANCE));
				Assert.Equal(session.CharacterId, receipt.Get<int>("playerObjectId"));
				Assert.Equal(player.GetEquipment().GetEquippedForAppearance().Select(i => i.GetItemSkinTemplate().GetTemplateId()).Order(),
					receipt.Get<BotEquipmentAppearance[]>("equipment").Select(i => i.SkinId).Order());
			}
			return Task.CompletedTask;
		}
		public async Task ReloginAsync(CancellationToken token)
		{
			await session.QuitAsync(token); await session.VerifyOfflineAsync(token); await session.WaitForReentryAsync(token);
			await session.ReloginAndVerifyPersistenceAsync(token); await session.EnterWorldAsync(token); await session.SynchronizeAsync(token);
		}
	}
}
