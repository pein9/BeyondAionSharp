using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Dao;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunS7Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		var token = timeout.Token;
		var sessions = Enumerable.Range(0, 2).Select(i => new SimulationL0Session(fixture, policy,
			$"b{i + 1:D2}", 75 + i, $"Aesimextended{(char)('a' + i)}", Race.ELYOS)).ToArray();
		try
		{
			foreach (var session in sessions)
			{
				session.BeginStep("s00", "login-create-enter-and-prepare-extended-social");
				await session.LoginAndAuthenticateAsync(token); await session.CreateCharacterAsync(token, PlayerClass.MAGE);
				await session.EnterWorldAsync(token); await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
				var player = fixture.World.GetPlayer(session.CharacterId);
				Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
				Assert.True(ClassChangeService.SetClass(player, PlayerClass.SPIRIT_MASTER, validate: true, updateDaevaStatus: true));
				player.GetCommonData().SetLevel(23);
				Assert.Equal(0, ItemService.AddItem(player, BotWorldModel.KinahItemId, ExtendedSocialScenario.SetupKinah));
				Assert.Equal(0, ItemService.AddItem(player, ExtendedSocialScenario.RecallReagent, 2));
				var point = SocialBasicsScenario.Registrar;
				await TeleportForSetupAsync(session, player, ExtendedSocialScenario.MapId, point.X - 5, point.Y, point.Z, token);
			}
			await ExtendedSocialScenario.RunAsync(sessions.Select(s => (IExtendedSocialDriver)new SimSocialDriver(fixture, s,
				fixture.World.GetPlayer(s.CharacterId))).ToArray(), "SimExtended", ct =>
			{
				// Same setup as the director's //legion setlevel; normal emblem operations require level two.
				LegionService.GetInstance().ChangeLevel(fixture.World.GetPlayer(sessions[0].CharacterId).GetLegion(), 2, true);
				return Task.CompletedTask;
			}, async ct =>
			{
				var point = ExtendedSocialScenario.WarehousePoint;
				foreach (var session in sessions)
					await TeleportForSetupAsync(session, fixture.World.GetPlayer(session.CharacterId), ExtendedSocialScenario.MapId,
						point.X - 3, point.Y, point.Z, ct);
			}, token);
			policy.AssertClean();
		}
		finally { foreach (var session in sessions) await session.DisposeAsync(); }
	}

	private async Task RunS6Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		var token = timeout.Token;
		var sessions = Enumerable.Range(0, 2).Select(i => new SimulationL0Session(fixture, policy,
			$"b{i + 1:D2}", 73 + i, $"Aesimgroupquest{(char)('a' + i)}", Race.ELYOS)).ToArray();
		try
		{
			foreach (var session in sessions)
			{
				session.BeginStep("s00", "login-create-enter-and-prepare-group-quest");
				await session.LoginAndAuthenticateAsync(token); await session.CreateCharacterAsync(token);
				await session.EnterWorldAsync(token); await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
				var player = fixture.World.GetPlayer(session.CharacterId);
				Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
				player.GetCommonData().SetLevel(9);
				var point = GroupQuestScenario.GiverPoint;
				await TeleportForSetupAsync(session, player, GroupQuestScenario.MapId, point.X - 2, point.Y, point.Z, token);
			}
			await GroupQuestScenario.RunAsync(sessions.Select(session => (IGroupQuestDriver)new SimSocialDriver(
				fixture, session, fixture.World.GetPlayer(session.CharacterId))).ToArray(), (npcId, ct) =>
			{
				var point = GroupQuestScenario.SpawnPoint;
				var spawn = Aion.GameServer.SpawnEngine.SpawnEngine.NewSpawn(GroupQuestScenario.MapId, npcId, point.X, point.Y, point.Z, point.Heading, 0);
				Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(spawn, fixture.World.GetPlayer(sessions[0].CharacterId).GetInstanceId());
				return Task.CompletedTask;
			}, token);
			policy.AssertClean();
		}
		finally { foreach (var session in sessions) await session.DisposeAsync(); }
	}

	private async Task RunS5Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		var token = timeout.Token;
		var sessions = Enumerable.Range(0, 3).Select(i => new SimulationL0Session(fixture, policy,
			$"b{i + 1:D2}", 70 + i, $"Assimgrouploot{(char)('a' + i)}", Race.ASMODIANS)).ToArray();
		try
		{
			foreach (var session in sessions)
			{
				session.BeginStep("s00", "login-create-enter-and-prepare-group-loot");
				await session.LoginAndAuthenticateAsync(token); await session.CreateCharacterAsync(token);
				await session.EnterWorldAsync(token); await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
				var player = fixture.World.GetPlayer(session.CharacterId);
				Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
				player.GetCommonData().SetLevel(2);
				var point = GroupLootScenario.SpawnPoint;
				await TeleportForSetupAsync(session, player, GroupLootScenario.MapId, point.X - 2, point.Y, point.Z, token);
			}
			var drivers = sessions.Select(session => (IGroupLootDriver)new SimSocialDriver(fixture, session, fixture.World.GetPlayer(session.CharacterId))).ToArray();
			await GroupLootScenario.RunAsync(drivers, ct =>
			{
				// Equivalent to the director's temporary //spawn, with the real template, AI and drop-registration path.
				var point = GroupLootScenario.SpawnPoint;
				var spawn = Aion.GameServer.SpawnEngine.SpawnEngine.NewSpawn(GroupLootScenario.MapId, GroupLootScenario.NpcId,
					point.X, point.Y, point.Z, point.Heading, 0);
				Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(spawn, fixture.World.GetPlayer(sessions[0].CharacterId).GetInstanceId());
				return Task.CompletedTask;
			}, token);
			policy.AssertClean();
		}
		finally { foreach (var session in sessions) await session.DisposeAsync(); }
	}

	private async Task RunS4Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		var token = timeout.Token;
		var sessions = Enumerable.Range(0, 8).Select(i => new SimulationL0Session(fixture, policy,
			$"b{i + 1:D2}", 62 + i, $"Aesimalliance{(char)('a' + i)}", Race.ELYOS)).ToArray();
		try
		{
			foreach (var session in sessions)
			{
				session.BeginStep("s00", "login-create-enter-alliance-subject");
				await session.LoginAndAuthenticateAsync(token);
				await session.CreateCharacterAsync(token);
				await session.EnterWorldAsync(token);
				await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
				Assert.Equal(0, fixture.World.GetPlayer(session.CharacterId).GetClientConnection().GetAccount().GetAccessLevel());
			}
			await AllianceLeagueScenario.RunAsync(sessions.Select(session => (IAllianceLeagueDriver)new SimSocialDriver(
				fixture, session, fixture.World.GetPlayer(session.CharacterId))).ToArray(), token);
			policy.AssertClean();
		}
		finally { foreach (var session in sessions) await session.DisposeAsync(); }
	}

	private async Task RunS3Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		var token = timeout.Token;
		await using var first = new SimulationL0Session(fixture, policy, "b01", 60, "Aesimfrienda", Race.ELYOS);
		await using var second = new SimulationL0Session(fixture, policy, "b02", 61, "Aesimfriendb", Race.ELYOS);
		foreach (var session in new[] { first, second })
		{
			session.BeginStep("s00", "login-create-enter-social-lists");
			await session.LoginAndAuthenticateAsync(token);
			await session.CreateCharacterAsync(token);
			await session.EnterWorldAsync(token);
			await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
			Assert.Equal(0, fixture.World.GetPlayer(session.CharacterId).GetClientConnection().GetAccount().GetAccessLevel());
		}
		await FriendsAndBlocksScenario.RunAsync(new SimSocialDriver(fixture, first, fixture.World.GetPlayer(first.CharacterId)),
			new SimSocialDriver(fixture, second, fixture.World.GetPlayer(second.CharacterId)), token);
		policy.AssertClean();
	}

	private async Task RunS2Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		var token = timeout.Token;
		await using var elyos = new SimulationL0Session(fixture, policy, "b01", 58, "Aesimpvp", Race.ELYOS);
		await using var asmodian = new SimulationL0Session(fixture, policy, "b02", 59, "Assimpvp", Race.ASMODIANS);
		foreach (var (session, point) in new[] { (elyos, PvpFlightScenario.ElyosStart), (asmodian, PvpFlightScenario.AsmodianStart) })
		{
			session.BeginStep("s00", "login-create-enter-and-prepare-pvp-flight");
			await session.LoginAndAuthenticateAsync(token);
			await session.CreateCharacterAsync(token, PlayerClass.MAGE);
			await session.EnterWorldAsync(token);
			await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
			var player = fixture.World.GetPlayer(session.CharacterId);
			Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
			Assert.True(ClassChangeService.SetClass(player, PlayerClass.SORCERER, validate: true, updateDaevaStatus: true));
			player.GetCommonData().SetLevel(10);
			Aion.GameServer.Services.Abyss.AbyssPointsService.AddAp(player, PvpFlightScenario.InitialAp);
			await TeleportForSetupAsync(session, player, PvpFlightScenario.MapId, point.X, point.Y, point.Z, token);
		}
		Assert.NotEqual(fixture.World.GetPlayer(elyos.CharacterId).GetRace(), fixture.World.GetPlayer(asmodian.CharacterId).GetRace());
		await PvpFlightScenario.RunAsync(new SimSocialDriver(fixture, elyos, fixture.World.GetPlayer(elyos.CharacterId)),
			new SimSocialDriver(fixture, asmodian, fixture.World.GetPlayer(asmodian.CharacterId)), token);
		policy.AssertClean();
	}

	private async Task RunS1Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		var token = timeout.Token;
		await using var first = new SimulationL0Session(fixture, policy, "b01", 56, "Aesimsociala", Race.ELYOS);
		await using var second = new SimulationL0Session(fixture, policy, "b02", 57, "Aesimsocialb", Race.ELYOS);
		int offset = 0;
		foreach (var session in new[] { first, second })
		{
			session.BeginStep("s00", "login-create-enter-and-prepare-social");
			await session.LoginAndAuthenticateAsync(token);
			await session.CreateCharacterAsync(token, PlayerClass.MAGE);
			await session.EnterWorldAsync(token);
			await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
			var player = fixture.World.GetPlayer(session.CharacterId);
			Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
			Assert.True(ClassChangeService.SetClass(player, PlayerClass.SORCERER, validate: true, updateDaevaStatus: true));
			player.GetCommonData().SetLevel(10);
			Assert.Equal(0, ItemService.AddItem(player, BotWorldModel.KinahItemId, 20_000));
			var point = SocialBasicsScenario.Registrar;
			await TeleportForSetupAsync(session, player, 110010000, point.X - 5 - offset, point.Y, point.Z, token);
			await session.MoveToPositionAsync(point with { X = point.X - 4 - offset }, token);
			offset++;
		}
		var firstDriver = new SimSocialDriver(fixture, first, fixture.World.GetPlayer(first.CharacterId));
		var secondDriver = new SimSocialDriver(fixture, second, fixture.World.GetPlayer(second.CharacterId));
		await SocialBasicsScenario.RunAsync(firstDriver, secondDriver, "SimSocial", token);
		foreach (var session in new[] { first, second }) session.BeginStep("s90", "natural-duel-recovery");
		await SocialBasicsScenario.RecoverForDuelAsync(firstDriver, secondDriver, token);
		foreach (var session in new[] { first, second })
		{
			var player = fixture.World.GetPlayer(session.CharacterId);
			Assert.Equal(player.GetLifeStats().GetMaxHp(), player.GetLifeStats().GetCurrentHp());
			Assert.Equal(player.GetLifeStats().GetMaxMp(), player.GetLifeStats().GetCurrentMp());
			session.BeginStep("s91", "repeat-duel-with-reversed-roles");
		}
		await SocialBasicsScenario.RunDuelAsync(secondDriver, firstDriver, Race.ELYOS, token);
		await firstDriver.VerifyAsync(second.CharacterId, "SimSocial", token);
		await secondDriver.VerifyAsync(first.CharacterId, "SimSocial", token);
		policy.AssertClean();
	}

	private sealed class SimSocialDriver(SimulationWorldFixture world, SimulationL0Session session, Player player) : ISocialBasicsDriver, IPvpFlightDriver, IFriendsAndBlocksDriver, IAllianceLeagueDriver, IGroupLootDriver, IGroupQuestDriver, IExtendedSocialDriver
	{
		private int step;
		public BotApi Api => session.Api;
		public int CharacterId => session.CharacterId;
		public string CharacterName => player.GetName();
		public BotPosition CurrentPosition => session.CurrentPosition;
		public IReadOnlyList<DecodedBotServerPacket> PacketHistory => session.PacketHistory;
		public async Task CompleteRecallTeleportAsync(CancellationToken token)
		{
			await WaitAsync(typeof(SM_CHANNEL_INFO), _ => true, token);
			await WaitAsync(typeof(SM_PLAYER_INFO), p => p.Get<int>("objectId") == CharacterId, token);
			await WaitAsync(typeof(SM_ABNORMAL_STATE), _ => true, token);
			session.AcceptTeleportPosition();
		}
		public Task VerifyExtendedSocialAsync(int legionId, CancellationToken token)
		{
			var current = world.World.GetPlayer(CharacterId); // Reconnect replaces the original Player instance.
			Assert.Equal(0, current.GetClientConnection().GetAccount().GetAccessLevel());
			Assert.False(current.IsDead()); Assert.False(current.IsInGroup());
			Assert.False(RecallService.GetInstance().HasPendingRequest(current));
			Assert.Equal(Api.World.Kinah, current.GetInventory().GetKinah());
			Assert.Equal(Api.World.Inventory.Values.Where(i => i.ItemId == ExtendedSocialScenario.RecallReagent).Sum(i => i.Count),
				current.GetInventory().GetItemCountByItemId(ExtendedSocialScenario.RecallReagent));
			var legion = current.GetLegion();
			Assert.Equal(legionId, legion.GetLegionId()); Assert.Equal(2, legion.GetLegionLevel());
			Assert.Equal(0, legion.GetLegionWarehouse().GetCurrentUser());
			Assert.Equal(ExtendedSocialScenario.Deposit - ExtendedSocialScenario.Withdrawal, legion.GetLegionWarehouse().GetKinah());
			var warehouseItems = InventoryDAO.LoadItems(legionId, Aion.GameServer.Model.Items.Storage.StorageType.LEGION_WAREHOUSE);
			var savedKinah = Assert.Single(warehouseItems);
			Assert.Equal(BotWorldModel.KinahItemId, savedKinah.GetItemId());
			Assert.Equal(ExtendedSocialScenario.Deposit - ExtendedSocialScenario.Withdrawal, savedKinah.GetItemCount());
			var emblem = legion.GetLegionEmblem();
			Assert.Equal(Api.World.LegionEmblems[legionId], new BotLegionEmblem(legionId, emblem.GetEmblemId(), emblem.GetEmblemType().GetValue(),
				emblem.GetColor_a(), emblem.GetColor_r(), emblem.GetColor_g(), emblem.GetColor_b()));
			var savedEmblem = LegionDAO.LoadLegionEmblem(legionId);
			Assert.Equal(Api.World.LegionEmblems[legionId], new BotLegionEmblem(legionId, savedEmblem.GetEmblemId(), savedEmblem.GetEmblemType().GetValue(),
				savedEmblem.GetColor_a(), savedEmblem.GetColor_r(), savedEmblem.GetColor_g(), savedEmblem.GetColor_b()));
			Assert.Null(savedEmblem.GetCustomEmblemData());
			var persisted = LegionDAO.LoadLegion(legionId);
			Assert.NotNull(persisted);
			LegionDAO.LoadHistory(persisted);
			foreach (var type in new[] { Aion.GameServer.Model.Team.Legion.LegionHistoryAction.Type.LEGION,
				Aion.GameServer.Model.Team.Legion.LegionHistoryAction.Type.WAREHOUSE })
			{
				var actual = legion.GetHistory(type); var saved = persisted.GetHistory(type);
				Assert.Equal(type == Aion.GameServer.Model.Team.Legion.LegionHistoryAction.Type.LEGION ? 4 : 2, saved.Count);
				Assert.Equal(actual.Select(e => (e.Id, e.Action, e.Name, e.Description)), saved.Select(e => (e.Id, e.Action, e.Name, e.Description)));
				// DAO returns floor(nowMillis/1000), while MySQL rounds fractional input into timestamp(0).
				for (int i = 0; i < actual.Count; i++) Assert.InRange(saved[i].EpochSeconds - actual[i].EpochSeconds, 0, 1);
			}
			return Task.CompletedTask;
		}
		public async Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token)
		{
			session.BeginStep($"s{++step:D2}", action);
			try { await operation(token); }
			catch (Exception exception)
			{
				string packets = string.Join(Environment.NewLine, session.PacketHistory.TakeLast(15).Select(packet =>
					packet.PacketType.Name + " " + System.Text.Json.JsonSerializer.Serialize(packet.Fields)));
				throw new InvalidOperationException($"Social step {action}, player {CharacterName}. Recent packets:{Environment.NewLine}{packets}", exception);
			}
		}
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => session.SendPacketAsync(packet, token);
		public async Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token)
		{
			await session.DrainServerPacketsAsync(token);
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
			timeout.CancelAfter(TimeSpan.FromSeconds(5));
			try { return await session.WaitForPacketAsync(type, timeout.Token, predicate); }
			catch (OperationCanceledException) when (!token.IsCancellationRequested)
			{
				throw new TimeoutException($"Social player {CharacterName} did not receive expected {type.Name} after the action/virtual-time advance.");
			}
		}
		public Task DelayAsync(TimeSpan duration, CancellationToken token) => session.AdvanceAsync(duration, token).AsTask();
		public async Task LogoutAsync(CancellationToken token)
		{
			await session.QuitAsync(token);
			await session.VerifyOfflineAsync(token);
		}
		public async Task ReenterAsync(CancellationToken token)
		{
			int packetStart = session.PacketHistory.Count;
			await session.WaitForReentryAsync(token);
			await session.ReloginAndVerifyPersistenceAsync(token);
			await session.EnterWorldAsync(token);
			await SynchronizeAsync(token);
			FriendsAndBlocksScenario.AssertReloadPackets(session.PacketHistory.Skip(packetStart));
		}
		public Task VerifyFriendsAndBlocksAsync(CancellationToken token)
		{
			// Resolve the reloaded Player, not the pre-relog instance captured for S1/S2.
			var current = world.World.GetPlayer(CharacterId);
			Assert.Equal(0, current.GetClientConnection().GetAccount().GetAccessLevel());
			foreach (var list in new[] { current.GetFriendList(), FriendListDAO.Load(current) })
			{
				Assert.Equal(Api.World.Friends.Count, list.GetSize());
				foreach (var friend in list)
				{
					Assert.True(Api.World.Friends.TryGetValue(friend.GetObjectId(), out var observed));
					Assert.Equal(observed.Name, friend.GetName()); Assert.Equal(observed.Memo, friend.GetFriendMemo());
				}
			}
			foreach (var list in new[] { current.GetBlockList(), BlockListDAO.Load(CharacterId) })
			{
				Assert.Equal(Api.World.BlockedPlayers.Count, list.GetSize());
				foreach (var blocked in list) Assert.Equal(Api.World.BlockedPlayers[blocked.GetName()], blocked.GetReason());
			}
			return Task.CompletedTask;
		}
		public Task VerifyAllianceAsync(CancellationToken token)
		{
			Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
			var alliance = player.GetPlayerAlliance();
			Assert.Equal(Api.World.AllianceId, alliance?.GetObjectId());
			if (alliance == null) return Task.CompletedTask;
			Assert.Null(player.GetPlayerGroup());
			Assert.Equal(Api.World.AllianceLeaderId, alliance.GetLeaderObject().GetObjectId());
			Assert.Equal(Api.World.AllianceMembers.Count, alliance.Size());
			Assert.Equal(Api.World.AllianceMembers.Keys.Order(), alliance.GetMembers().Select(p => p.GetObjectId()).Order());
			Assert.Equal(Api.World.AllianceViceCaptains.Order(), alliance.GetViceCaptainIds().Order());
			foreach (var row in Api.World.AllianceMembers.Values)
				Assert.Equal(row.GroupId, alliance.GetMember(row.ObjectId).GetAllianceId());
			var league = alliance.GetLeague();
			Assert.Equal(Api.World.LeagueId, league?.GetTeamId());
			if (league != null)
			{
				Assert.Equal(Api.World.LeagueAlliances.Count, league.Size());
				Assert.Equal(Api.World.LeagueAlliances.Keys.Order(), league.GetMembers().Select(a => a.GetObjectId()).Order());
				foreach (var row in Api.World.LeagueAlliances.Values)
				{
					var member = league.GetMember(row.AllianceId);
					Assert.Equal(row.Position, member.GetLeaguePosition()); Assert.Equal(row.MemberCount, member.GetObject().Size());
					Assert.Equal(row.CaptainName, member.GetObject().GetLeaderObject().GetName());
				}
			}
			return Task.CompletedTask;
		}
		public Task VerifyLootAsync(int corpseId, IReadOnlyList<int> allowedLooters, bool collected, CancellationToken token)
		{
			Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
			Assert.False(player.IsDead());
			Assert.False(player.IsInPlayerMode(Aion.GameServer.Model.Actions.PlayerMode.IN_ROLL));
			if (!collected)
			{
				var corpse = Assert.IsAssignableFrom<Aion.GameServer.Model.GameObjects.Npc>(world.World.FindVisibleObject(corpseId));
				Assert.True(corpse.IsDead());
				var drops = Aion.GameServer.Services.Drop.DropRegistrationService.GetInstance().GetDropRegistrationMap()[corpseId];
				Assert.Equal(allowedLooters.Order(), drops.GetAllowedLooters().Order());
				Assert.Equal(3, drops.GetInRangePlayers().Count);
				Assert.Equal(Api.World.GroupMembers.Keys.Order(), drops.GetInRangePlayers().Select(p => p.GetObjectId()).Order());
				Assert.Equal(Api.World.GroupId, drops.GetLootingTeamId());
			}
			else
			{
				// Other random drops may keep the corpse alive; the guaranteed stack must be gone either way.
				var registration = Aion.GameServer.Services.Drop.DropRegistrationService.GetInstance();
				if (registration.GetCurrentDropMap().TryGetValue(corpseId, out var remaining))
					Assert.DoesNotContain(remaining, item => item.GetDropTemplate().GetItemId() == GroupLootScenario.ItemId);
				if (world.World.FindVisibleObject(corpseId) is Aion.GameServer.Model.GameObjects.Npc corpse)
					Assert.True(corpse.IsDead());
			}
			var rules = player.GetPlayerGroup().GetLootGroupRules();
			Assert.Equal(Api.World.GroupLootRules, new[] { rules.GetLootRule().GetId(), rules.GetMisc(), rules.GetCommonItemAbove(),
				rules.GetSuperiorItemAbove(), rules.GetHeroicItemAbove(), rules.GetFabledItemAbove(), rules.GetEternalItemAbove(), rules.GetMythicItemAbove() });
			Assert.Equal(Api.World.Inventory.Values.Where(item => item.ItemId == GroupLootScenario.ItemId).Sum(item => item.Count),
				player.GetInventory().GetItemCountByItemId(GroupLootScenario.ItemId));
			return Task.CompletedTask;
		}
		public Task VerifyGroupQuestAsync(int? corpseId, int? attackerId, CancellationToken token)
		{
			Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
			Assert.False(player.IsDead());
			var state = player.GetQuestStateList().GetQuestState(GroupQuestScenario.QuestId);
			Assert.NotNull(state);
			Assert.Equal(Api.World.Quests[GroupQuestScenario.QuestId].Status, (byte)state.GetStatus());
			Assert.Equal(Api.World.Quests[GroupQuestScenario.QuestId].StepAndFlags, state.GetQuestVars().GetQuestVars() | state.GetFlags() << 24);
			Assert.Equal(Api.World.Kinah, player.GetInventory().GetKinah());
			Assert.Equal(Api.World.Inventory.Values.Where(i => i.ItemId == GroupQuestScenario.RewardItem).Sum(i => i.Count),
				player.GetInventory().GetItemCountByItemId(GroupQuestScenario.RewardItem));
			if (corpseId is int id)
			{
				var corpse = Assert.IsAssignableFrom<Aion.GameServer.Model.GameObjects.Npc>(world.World.FindVisibleObject(id));
				Assert.True(corpse.IsDead());
				var damage = Assert.Single(corpse.GetAggroList().GetFinalDamageList().GetCreatureDamages());
				Assert.Equal(attackerId, damage.GetAttacker().GetObjectId());
				Assert.True(damage.GetDamage() > 0);
			}
			return Task.CompletedTask;
		}
		public Task MoveAsync(BotPosition position, CancellationToken token) => session.MoveToPositionAsync(position, token);
		public Task FlyAsync(BotPosition position, CancellationToken token) => session.ExecuteMovementAsync(
			new Aion.Bots.Movement.BotMover(Api.World, Api.Timing).CreateFlightPlan([position]), token);
		public Task VerifyPvpAsync(bool winner, CancellationToken token)
		{
			Assert.Equal(!winner, player.IsDead());
			Assert.Equal(winner ? 800 : 410, player.GetAbyssRank().GetAp());
			Assert.Equal(winner ? 1 : 0, player.GetAbyssRank().GetAllKill());
			Assert.False(player.IsInGroup());
			Assert.False(DuelService.GetInstance().IsDueling(player));
			Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
			Assert.InRange(Math.Abs(player.GetX() - CurrentPosition.X), 0, 0.1f);
			Assert.InRange(Math.Abs(player.GetY() - CurrentPosition.Y), 0, 0.1f);
			Assert.InRange(Math.Abs(player.GetZ() - CurrentPosition.Z), 0, 0.1f);
			return Task.CompletedTask;
		}
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await SendAsync(GameClientPackets.TimeCheck(unchecked((int)world.Clock.NowMillis)), token);
			await WaitAsync(typeof(SM_TIME_CHECK), _ => true, token);
		}
		public Task VerifyAsync(int otherId, string legionName, CancellationToken token)
		{
			Assert.False(player.IsDead());
			Assert.False(player.IsInGroup());
			Assert.False(DuelService.GetInstance().IsDueling(player));
			Assert.Null(player.GetInteractionTask());
			Assert.Equal(legionName, player.GetLegion().GetName());
			Assert.Equal(player.GetLegion().GetLegionId(), world.World.GetPlayer(otherId).GetLegion().GetLegionId());
			Assert.Equal(new[] { CharacterId, otherId }.Order(), LegionMemberDAO.LoadLegionMembers(player.GetLegion().GetLegionId()).Order());
			var persisted = LegionMemberDAO.LoadLegionMember(CharacterId);
			Assert.NotNull(persisted);
			Assert.Equal(player.GetLegionMember().GetRank(), persisted.GetRank());
			LegionDAO.LoadHistory(player.GetLegion());
			var history = player.GetLegion().GetHistory(Aion.GameServer.Model.Team.Legion.LegionHistoryAction.Type.LEGION);
			Assert.Equal(3, history.Count);
			Assert.Equal(new[] { "CREATE:", $"JOIN:{CharacterName}", $"JOIN:{world.World.GetPlayer(otherId).GetName()}" }.Order(),
				history.Select(entry => $"{entry.Action}:{entry.Name}").Order());
			Assert.Equal(Api.World.Kinah, player.GetInventory().GetKinah());
			return Task.CompletedTask;
		}
	}
}
