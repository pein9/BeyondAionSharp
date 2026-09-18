using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunE4Async(ScenarioDefinition scenario, bool includeHistory)
		=> await RunCookingAsync(scenario, includeHistory, workOrders: false);
	private async Task RunE5Async(ScenarioDefinition scenario, bool includeHistory)
		=> await RunCookingAsync(scenario, includeHistory, workOrders: true);

	private async Task RunCookingAsync(ScenarioDefinition scenario, bool includeHistory, bool workOrders)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		var token = timeout.Token;
		int failChance = CraftConfig.MAX_CRAFT_FAILURE_CHANCE;
		CraftConfig.MAX_CRAFT_FAILURE_CHANCE = 0;
		try
		{
			foreach (var (master, baseAccount, baseName) in new[]
			{
				(CookingMaster.Hestia, 47, "Aesimcooking"),
				(CookingMaster.Lainita, 48, "Assimcooking"),
			})
			{
				int account = baseAccount + (workOrders ? 2 : 0);
				string name = baseName + (workOrders ? "work" : "");
				await using var session = new SimulationL0Session(fixture, policy, $"b{baseAccount - 46:D2}", account, name, master.Race);
				session.BeginStep("s00", "login-create-enter");
				await session.LoginAndAuthenticateAsync(token);
				await session.CreateCharacterAsync(token);
				await session.EnterWorldAsync(token);
				await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
				var player = fixture.World.GetPlayer(session.CharacterId);
				Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
				var driver = new SimCookingDriver(this, fixture, session, player);
				await CookingLearnScenario.RunAsync(driver, master, token);
				if (workOrders)
					foreach (var order in CookingWorkOrder.For(master))
						await CookingWorkOrderScenario.RunAsync(driver, order, token);
			}
			policy.AssertClean();
		}
		finally { CraftConfig.MAX_CRAFT_FAILURE_CHANCE = failChance; }
	}

	private sealed class SimCookingDriver(SimulationFastScenarioTests owner, SimulationWorldFixture world,
		SimulationL0Session session, Player player) : ICookingWorkOrderDriver
	{
		private int step;
		public BotApi Api => session.Api;
		public IReadOnlyList<DecodedBotServerPacket> History => session.PacketHistory;
		public async Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token)
		{
			session.BeginStep($"s{++step:D2}", action);
			try { await operation(token); }
			catch (Exception exception)
			{
				string packets = string.Join(Environment.NewLine, History.TakeLast(20).Select(packet =>
					packet.PacketType.Name + " " + System.Text.Json.JsonSerializer.Serialize(packet.Fields)));
				throw new InvalidOperationException($"Cooking step {action} failed. Recent packets:{Environment.NewLine}{packets}", exception);
			}
		}
		public async Task<int> PrepareAsync(CookingMaster master, CancellationToken token)
		{
			player.GetCommonData().SetLevel(9);
			player.GetInventory().IncreaseKinah(5000);
			var point = master.Position;
			await owner.TeleportForSetupAsync(session, player, master.MapId, point.X - 5, point.Y, point.Z, token);
			var npc = player.GetPosition().GetWorldMapInstance().GetNpcs(master.NpcId).First(npc => npc.IsSpawned());
			await session.MoveToPositionAsync(new BotPosition(npc.GetX() - 1, npc.GetY(), npc.GetZ(), 0), token);
			return npc.GetObjectId();
		}
		public Task MakeLevelTenAsync(CancellationToken token)
		{
			Assert.True(ClassChangeService.SetClass(player, PlayerClass.GLADIATOR, validate: true, updateDaevaStatus: true));
			player.GetCommonData().SetLevel(10);
			return Task.CompletedTask;
		}
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => session.SendPacketAsync(packet, token);
		public Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) =>
			session.WaitForPacketAsync(type, token, predicate);
		public Task DelayAsync(TimeSpan delay, CancellationToken token) => session.AdvanceAsync(delay, token).AsTask();
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await SendAsync(GameClientPackets.TimeCheck(unchecked((int)world.Clock.NowMillis)), token);
			await WaitAsync(typeof(SM_TIME_CHECK), _ => true, token);
		}
		public Task VerifyServerStateAsync(CancellationToken token)
		{
			Assert.Null(player.GetInteractionTask());
			Assert.False(player.GetResponseRequester().Respond(CookingLearnScenario.QuestionCode, 0));
			Assert.Equal(1, player.GetSkillList().GetSkillLevel(CookingLearnScenario.SkillId));
			Assert.Equal(Api.World.Kinah, player.GetInventory().GetKinah());
			Assert.Equal(Api.World.Recipes.Order(), player.GetRecipeList().GetRecipeList().Order());
			Assert.Equal(Api.World.Recipes.Order(), PlayerRecipesDAO.Load(player.GetObjectId()).GetRecipeList().Order());
			return Task.CompletedTask;
		}
		public Task PrepareWorkOrderAsync(CookingWorkOrder order, CancellationToken token)
		{
			if (order.NeedsSalt)
			{
				player.GetSkillList().AddSkill(player, CookingLearnScenario.SkillId, order.SkillLevel);
				Assert.Equal(0, ItemService.AddItem(player, CookingWorkOrder.SaltId, order.DeliverCount + 1));
			}
			return Task.CompletedTask;
		}
		public Task MoveBesideAsync(int objectId, CancellationToken token)
		{
			BotPosition point = Api.World.Objects[objectId].Position;
			return session.MoveToPositionAsync(new BotPosition(point.X - 1, point.Y, point.Z, 0), token);
		}
		public async Task AwaitCraftAsync(CancellationToken token)
		{
			Assert.NotNull(player.GetInteractionTask());
			long deadline = world.Clock.NowMillis + 90_000;
			while (player.GetInteractionTask() != null)
			{
				long now = world.Clock.NowMillis;
				long next = world.Clock.NextDueMillis ?? throw new InvalidDataException("Craft has no scheduled work.");
				Assert.InRange(next, now, deadline);
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(next - now), token);
			}
		}
		public Task VerifyWorkOrderAsync(CookingWorkOrder order, CancellationToken token)
		{
			Assert.Null(player.GetInteractionTask());
			Assert.Equal(QuestStatus.COMPLETE, player.GetQuestStateList().GetQuestState(order.QuestId).GetStatus());
			Assert.Equal(Api.World.Recipes.Order(), PlayerRecipesDAO.Load(player.GetObjectId()).GetRecipeList().Order());
			var actual = player.GetInventory().GetItemsWithKinah().Concat(player.GetEquipment().GetEquippedItems())
				.GroupBy(item => item.GetItemId()).ToDictionary(group => group.Key, group => group.Sum(item => item.GetItemCount()));
			var observed = Api.World.Inventory.Values.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
			Assert.Equal(actual.OrderBy(pair => pair.Key), observed.OrderBy(pair => pair.Key));
			return Task.CompletedTask;
		}
	}
}
