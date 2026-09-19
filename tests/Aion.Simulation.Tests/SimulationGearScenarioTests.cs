using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.Commons.Database;
using Aion.GameServer.Dao;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunG1Async(ScenarioDefinition scenario, bool includeHistory)
		=> await RunGearAsync(scenario, includeHistory, godstone: false);
	private async Task RunG2Async(ScenarioDefinition scenario, bool includeHistory)
		=> await RunGearAsync(scenario, includeHistory, godstone: true);
	private async Task RunG3Async(ScenarioDefinition scenario, bool includeHistory)
		=> await RunGearAsync(scenario, includeHistory, godstone: false, fusion: true);
	private async Task RunG4Async(ScenarioDefinition scenario, bool includeHistory)
		=> await RunGearAsync(scenario, includeHistory, godstone: false, binding: true);
	private async Task RunG5Async(ScenarioDefinition scenario, bool includeHistory)
		=> await RunGearAsync(scenario, includeHistory, godstone: false, upgrade: true);
	private async Task RunG6Async(ScenarioDefinition scenario, bool includeHistory)
		=> await RunGearAsync(scenario, includeHistory, godstone: false, utility: true);
	private async Task RunGearAsync(ScenarioDefinition scenario, bool includeHistory, bool godstone, bool fusion = false, bool binding = false, bool upgrade = false, bool utility = false)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		var token = timeout.Token;
		await using var session = new SimulationL0Session(fixture, policy, "b01", utility ? 82 : upgrade ? 81 : binding ? 80 : fusion ? 79 : godstone ? 78 : 77,
			utility ? "Aesimutility" : upgrade ? "Aesimupgrade" : binding ? "Aesimstigma" : fusion ? "Aesimfusion" : godstone ? "Aesimgodstone" : "Aesimgear");
		session.BeginStep("s00", "login-create-enter");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token);
		await session.EnterWorldAsync(token);
		await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
		var driver = new SimGearDriver(this, fixture, session, godstone, fusion, binding, upgrade, utility);
		if (utility) await InventoryUtilityScenario.RunAsync(driver, token);
		else if (upgrade) await GearUpgradeScenario.RunAsync(driver, token);
		else if (binding) await SoulBindStigmaScenario.RunAsync(driver, token);
		else if (fusion) await WeaponFusionScenario.RunAsync(driver, token);
		else if (godstone) await GodstoneScenario.RunAsync(driver, token);
		else await GearSocketScenario.RunAsync(driver, token);
		policy.AssertClean();
	}

	private sealed class SimGearDriver(SimulationFastScenarioTests owner, SimulationWorldFixture world,
		SimulationL0Session session, bool godstone, bool fusion, bool binding, bool upgrade, bool utility) : IGodstoneDriver, IGearUpgradeDriver
	{
		private int step;
		public BotApi Api => session.Api;
		public IReadOnlyList<DecodedBotServerPacket> History => session.PacketHistory;
		public async Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token)
		{
			session.BeginStep($"s{++step:D2}", action);
			Console.WriteLine($"{(utility ? "G6" : upgrade ? "G5" : binding ? "G4" : fusion ? "G3" : godstone ? "G2" : "G1")} s{step:D2}: {action}");
			try { await operation(token); }
			catch (Exception exception)
			{
				var player = world.World.GetPlayer(session.CharacterId);
				string state = player == null ? "Player absent" :
					$"race={player.GetRace()}, class={player.GetPlayerClass()}, level={player.GetLevel()}, " +
					$"stigmaQuest={player.GetQuestStateList().GetQuestState(SoulBindStigmaScenario.UnlockQuestId)?.GetStatus()}, " +
					$"itemUse={player.GetController().HasScheduledTask(TaskId.ITEM_USE)}, " +
					$"stigmaInCube={player.GetInventory().GetItemsByItemId(SoulBindStigmaScenario.StigmaId).Count}";
				string packets = string.Join(Environment.NewLine, session.PacketHistory.TakeLast(20).Select(packet =>
					packet.PacketType.Name + " " + System.Text.Json.JsonSerializer.Serialize(packet.Fields)));
				throw new InvalidOperationException($"Gear step {action} failed. {state}. Recent packets:{Environment.NewLine}{packets}", exception);
			}
		}
		public async Task<int> PrepareAsync(CancellationToken token)
		{
			var player = world.World.GetPlayer(session.CharacterId);
			Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
			Assert.True(ClassChangeService.SetClass(player, PlayerClass.GLADIATOR, validate: true, updateDaevaStatus: true));
			if (utility)
			{
				player.GetCommonData().SetLevel(65);
				foreach (var grant in InventoryUtilityScenario.Grants)
					Assert.Equal(0, ItemService.AddItem(player, grant.Id, grant.Count));
				var origin = InventoryUtilityScenario.Position;
				await owner.TeleportForSetupAsync(session, player, InventoryUtilityScenario.MapId, origin.X - 5, origin.Y, origin.Z, token);
				return await ApproachServiceAsync(InventoryUtilityScenario.NpcId, token);
			}
			if (upgrade)
			{
				player.GetCommonData().SetLevel(65);
				foreach (var grant in GearUpgradeScenario.Grants)
					Assert.Equal(0, ItemService.AddItem(player, grant.Id, grant.Count));
				var origin = GearUpgradeScenario.Position;
				await owner.TeleportForSetupAsync(session, player, GearUpgradeScenario.MapId, origin.X - 5, origin.Y, origin.Z, token);
				return await ApproachServiceAsync(GearUpgradeScenario.RemodelNpcId, token);
			}
			player.GetCommonData().SetLevel(binding ? 20 : 16);
			Assert.Equal(0, ItemService.AddItem(player, BotWorldModel.KinahItemId, 100_000));
			Assert.Equal(0, ItemService.AddItem(player, binding ? SoulBindStigmaScenario.WeaponId : fusion ? WeaponFusionScenario.WeaponId : GearSocketScenario.WeaponId,
				binding ? 1 : fusion ? 2 : godstone ? 1 : GearSocketScenario.WeaponCount));
			if (binding)
			{
				Assert.Equal(0, ItemService.AddItem(player, SoulBindStigmaScenario.StigmaId, 1));
				// Level/class setup may already start this campaign. Match the director's quest-set command:
				// update the existing state instead of silently ignoring a rejected duplicate AddQuest.
				var quests = player.GetQuestStateList();
				var quest = quests.GetQuestState(SoulBindStigmaScenario.UnlockQuestId);
				if (quest == null)
				{
					quest = new Aion.GameServer.QuestEngine.Model.QuestState(SoulBindStigmaScenario.UnlockQuestId, Aion.GameServer.QuestEngine.Model.QuestStatus.COMPLETE);
					Assert.True(quests.AddQuest(SoulBindStigmaScenario.UnlockQuestId, quest));
				}
				else quest.SetStatus(Aion.GameServer.QuestEngine.Model.QuestStatus.COMPLETE);
				quest.SetQuestVar(0);
				quest.SetRewardGroup(0);
				Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnQuestCompleted(player, SoulBindStigmaScenario.UnlockQuestId);
				Assert.True(player.IsCompleteQuest(SoulBindStigmaScenario.UnlockQuestId));
			}
			else if (fusion)
				Assert.Equal(0, ItemService.AddItem(player, GearSocketScenario.ManastoneId, WeaponFusionScenario.StoneCount));
			else if (godstone)
				Assert.Equal(0, ItemService.AddItem(player, GodstoneScenario.GodstoneId, 1));
			else
			{
				Assert.Equal(0, ItemService.AddItem(player, GearSocketScenario.ManastoneId, GearSocketScenario.StoneCount));
				Assert.Equal(0, ItemService.AddItem(player, GearSocketScenario.EnchantStoneId, GearSocketScenario.StoneCount));
			}
			var point = fusion ? WeaponFusionScenario.Position : godstone ? GodstoneScenario.Position : GearSocketScenario.Position;
			await owner.TeleportForSetupAsync(session, player, godstone ? GodstoneScenario.MapId : GearSocketScenario.MapId, point.X - 5, point.Y, point.Z, token);
			if (binding) return 0; // Binding and stigma equipping do not require an NPC.
			if (godstone)
			{
				var spawn = Aion.GameServer.SpawnEngine.SpawnEngine.NewSpawn(GodstoneScenario.MapId, GodstoneScenario.DummyId,
					point.X, point.Y, point.Z, point.Heading, 0);
				var dummy = Assert.IsType<Aion.GameServer.Model.GameObjects.Npc>(Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(spawn, player.GetInstanceId()));
				Assert.True(player.IsEnemy(dummy));
				await session.MoveToPositionAsync(point with { X = point.X - 1 }, token);
				return dummy.GetObjectId();
			}
			var npc = player.GetPosition().GetWorldMapInstance().GetNpcs(fusion ? WeaponFusionScenario.NpcId : GearSocketScenario.RemoverNpcId).First(npc => npc.IsSpawned());
			await session.MoveToPositionAsync(new BotPosition(npc.GetX() - 1, npc.GetY(), npc.GetZ(), 0), token);
			return npc.GetObjectId();
		}
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => session.SendPacketAsync(packet, token);
		public async Task<int> ApproachServiceAsync(int npcTemplateId, CancellationToken token)
		{
			var player = world.World.GetPlayer(session.CharacterId);
			var npc = player.GetPosition().GetWorldMapInstance().GetNpcs(npcTemplateId).First(npc => npc.IsSpawned());
			Assert.True(npc.GetObjectTemplate().SupportsAction(npcTemplateId == InventoryUtilityScenario.NpcId ? DialogAction.EXTEND_INVENTORY : npcTemplateId == GearUpgradeScenario.RemodelNpcId ? 43 : 75));
			await session.MoveToPositionAsync(new BotPosition(npc.GetX() - 1, npc.GetY(), npc.GetZ(), 0), token);
			return npc.GetObjectId();
		}
		public async Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token)
		{
			await session.DrainServerPacketsAsync(token);
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
			timeout.CancelAfter(TimeSpan.FromSeconds(5));
			try { return await session.WaitForPacketAsync(type, timeout.Token, predicate); }
			catch (OperationCanceledException) when (!token.IsCancellationRequested)
			{
				throw new TimeoutException($"Gear scenario did not receive {type.Name} after its action/virtual-time advance.");
			}
		}
		public Task DelayAsync(TimeSpan delay, CancellationToken token) => session.AdvanceAsync(delay, token).AsTask();
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await SendAsync(GameClientPackets.TimeCheck(unchecked((int)world.Clock.NowMillis)), token);
			await WaitAsync(typeof(SM_TIME_CHECK), _ => true, token);
		}
		public async Task VerifyPersistenceAsync(CancellationToken token)
		{
			var expected = Api.World.Inventory.OrderBy(pair => pair.Key).ToArray();
			var cube = Api.World.CubeExpansion;
			await session.QuitAsync(token);
			await session.VerifyOfflineAsync(token);
			await session.WaitForReentryAsync(token);
			await session.ReloginAndVerifyPersistenceAsync(token);
			await session.EnterWorldAsync(token);
			await SynchronizeAsync(token);
			Assert.Equal(expected, Api.World.Inventory.OrderBy(pair => pair.Key));
			if (utility)
			{
				Assert.NotNull(cube);
				Assert.Equal(cube, Api.World.CubeExpansion);
				Assert.Equal(cube.Capacity, world.World.GetPlayer(session.CharacterId).GetInventory().GetLimit());
				using var cubeConnection = DatabaseFactory.GetConnection();
				cubeConnection.Open();
				using var cubeCommand = cubeConnection.CreateCommand();
				cubeCommand.CommandText = "SELECT npc_expands, quest_expands, item_expands FROM players WHERE id=@id";
				cubeCommand.Parameters.AddWithValue("@id", session.CharacterId);
				using var reader = cubeCommand.ExecuteReader();
				Assert.True(reader.Read());
				Assert.Equal(cube.Npc, reader.GetByte(0));
				Assert.Equal(cube.Quest, reader.GetByte(1));
				Assert.Equal(cube.Item, reader.GetByte(2));
				Assert.False(reader.Read());
			}
			Assert.Equal(0, world.World.GetPlayer(session.CharacterId).GetClientConnection().GetAccount().GetAccessLevel());
			var saved = InventoryDAO.LoadItems(session.CharacterId, StorageType.CUBE);
			Assert.Equal(expected.Length, saved.Count);
			ItemStoneListDAO.Load(saved);
			foreach (var item in saved)
			{
				var observed = Api.World.Inventory[item.GetObjectId()];
				Assert.Equal(observed.ItemId, item.GetItemId());
				Assert.Equal(observed.Count, item.GetItemCount());
				Assert.Equal(observed.Details.PackCount ?? 0, unchecked((byte)item.GetPackCount()));
				if (observed.Details.ChargePoints is { } chargePoints) Assert.Equal(chargePoints, item.GetChargePoints());
				if (observed.Details.Premium is { } premium)
				{
					Assert.Equal(premium.BonusStatsId, unchecked((byte)(item.IsIdentified() ? item.GetBonusStatsId() : -1)));
					Assert.Equal(premium.TuneCount, unchecked((byte)(item.IsIdentified() ? item.GetTuneCount() : 0)));
				}
				if (observed.Details.Enchantment is not { } enchantment) continue;
				Assert.Equal(enchantment.EnchantLevel, item.GetEnchantLevel());
				Assert.Equal(enchantment.SoulBound, item.IsSoulBound());
				Assert.Equal(enchantment.SkinId, item.GetItemSkinTemplate().GetTemplateId());
				Assert.Equal(enchantment.OptionalSockets, unchecked((byte)(item.IsIdentified() ? item.GetOptionalSockets() : -1)));
				Assert.Equal(enchantment.EnchantBonus, unchecked((byte)(item.IsIdentified() ? item.GetEnchantBonus() : -1)));
				Assert.Equal(enchantment.GodstoneId, item.GetGodStoneId());
				var stones = item.HasManaStones() ? item.GetItemStones().ToDictionary(stone => stone.GetSlot(), stone => stone.GetItemId()) : [];
				Assert.Equal(enchantment.Manastones.ToList(), Enumerable.Range(0, 6).Select(slot => stones.GetValueOrDefault(slot)));
				if (observed.Details.Fusion is { } fused)
				{
					Assert.Equal(fused.ItemId, item.GetFusionedItemId());
					Assert.Equal(fused.OptionalSockets, item.GetFusionedItemOptionalSockets());
					Assert.Equal(fused.BonusStatsId, item.GetFusionedItemBonusStatsId());
					var fusionStones = item.HasFusionStones() ? item.GetFusionStones().ToDictionary(stone => stone.GetSlot(), stone => stone.GetItemId()) : [];
					Assert.Equal(fused.Manastones.ToList(), Enumerable.Range(0, 6).Select(slot => fusionStones.GetValueOrDefault(slot)));
				}
			}
			using var connection = DatabaseFactory.GetConnection();
			connection.Open();
			using var command = connection.CreateCommand();
			command.CommandText = "SELECT COUNT(*) FROM item_stones s LEFT JOIN inventory i ON i.item_unique_id=s.item_unique_id WHERE i.item_unique_id IS NULL";
			Assert.Equal(0L, Convert.ToInt64(command.ExecuteScalar()));
			if (binding)
			{
				var player = world.World.GetPlayer(session.CharacterId);
				Assert.Equal(Api.World.Skills.ContainsKey(SoulBindStigmaScenario.SkillId), player.GetSkillList().IsSkillPresent(SoulBindStigmaScenario.SkillId));
			}
		}
		public Task VerifyProcAsync(int targetObjectId, bool active, CancellationToken token)
		{
			var player = world.World.GetPlayer(session.CharacterId);
			var dummy = Assert.IsType<Aion.GameServer.Model.GameObjects.Npc>(world.World.FindVisibleObject(targetObjectId));
			Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
			Assert.Equal(GodstoneScenario.AttackSpeedMillis, player.GetGameStats().GetAttackSpeed().GetCurrent());
			Assert.Equal(GodstoneScenario.GodstoneId, player.GetEquipment().GetMainHandWeapon().GetGodStoneId());
			// Proc observation is at +1.45s, before poison's first tick at +2.3s; the dummy can
			// also regenerate the preceding normal-attack damage. The shared window requires all
			// ten negative damage notifications and cessation, not missing HP at this premature sample.
			Assert.False(dummy.IsDead());
			Assert.Equal(active, dummy.GetEffectController().HasAbnormalEffect(GodstoneScenario.SkillId));
			return Task.CompletedTask;
		}
	}
}
