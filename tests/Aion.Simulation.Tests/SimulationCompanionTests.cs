using Aion.Commons.Database;
using Aion.GameServer.Custom.Companion;
using Aion.GameServer.Dao;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// The companion server tests play in a process of their own: <c>AION_SIM_COMPANION=1</c> with a filter on
	/// "Companion". The tests of a process share one world, one clock and one seeded random source. These make and
	/// delete characters and move the clock by minutes, and with them in the Fast process two unrelated fights
	/// there came out differently (run l1-fast).
	/// </summary>
	private const string CompanionServerTestsSwitch = "AION_SIM_COMPANION";

	private static void SkipUnlessCompanionServerTests() => Skip.If(Environment.GetEnvironmentVariable(CompanionServerTestsSwitch) != "1",
		$"Set {CompanionServerTestsSwitch}=1 to play the companion server tests in a process of their own.");

	/// <summary>
	/// D41 (docs/companion-bots.md, C2): the server's part of the companion bots, which Java does not have. While the
	/// feature is off <c>.companion</c> is no command. Switched on, it makes a level-26 character of the class from its
	/// generated profile where the player stands, on an account a host offered, and leaves a request for that host. The
	/// character logs in as any other and is in the player's group, which the player leads, from the moment it enters the
	/// world (C3), and the group's loot is free for all from then on (C6). The server looks at its companions every
	/// ten seconds (C9): one whose player has left the group ends, one that stays after it was sent away is
	/// disconnected, and one that nobody logged in is deleted in time. One that is sent away, or whose player logs out, is deleted with its rows when it
	/// has left the world; one that nobody came to play is deleted at once.
	/// </summary>
	[SkippableFact]
	public async Task CompanionCommandMakesAThrowawayCharacterAndDeletesIt()
	{
		SkipUnlessCompanionServerTests();
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		using var policy = NewPolicy("D41", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		CancellationToken token = timeout.Token;
		CompanionService service = CompanionService.GetInstance();
		int[] seats = [102, 103, 104, 105, 106, 107];
		await using var session = new SimulationL0Session(fixture, policy, "b01", 101, "Asimleader", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player leader = fixture.World.GetPlayer(session.CharacterId);
		leader.SetBindPoint(new BindPointPosition(220010000, 571f, 2787f, 299f, 30));

		async Task<string[]> SayAsync(string text)
		{
			int start = session.PacketHistory.Count;
			await session.SendPacketAsync(session.Api.Say(text), token);
			await session.SynchronizeAsync(token);
			return session.PacketHistory.Skip(start).Where(packet => packet.PacketType == typeof(SM_MESSAGE))
				.Select(packet => packet.Get<string>("message")).ToArray();
		}
		static long Rows(string table, string column, int id)
		{
			using var connection = DatabaseFactory.GetConnection(); connection.Open();
			using var command = connection.CreateCommand();
			command.CommandText = $"SELECT COUNT(*) FROM {table} WHERE {column}=@id";
			command.Parameters.AddWithValue("@id", id);
			return Convert.ToInt64(command.ExecuteScalar());
		}
		static void AssertGone(int characterId)
		{
			foreach ((string table, string column) in new[] { ("players", "id"), ("player_appearance", "player_id"), ("player_skills", "player_id"),
				("inventory", "item_owner"), ("player_quests", "player_id"), ("player_bind_point", "player_id") })
				Assert.True(Rows(table, column, characterId) == 0, $"{table} still has rows of the deleted companion {characterId}.");
		}

		try
		{
			session.BeginStep("s01", "off-is-no-command");
			Assert.False(service.Enabled);
			Assert.DoesNotContain(await SayAsync(".companion cleric"), message => message.Contains("on its way", StringComparison.Ordinal));
			Assert.Empty(service.All());

			session.BeginStep("s02", "refusals");
			service.Enable();
			Assert.Contains(await SayAsync(".companion bogus"), message => message.Contains("There is no class called bogus.", StringComparison.Ordinal));
			Assert.Contains(await SayAsync(".companion cleric"), message => message.Contains("No companion is free right now.", StringComparison.Ordinal));
			Assert.Contains(await SayAsync(".companion list"), message => message.Contains("You have no companion.", StringComparison.Ordinal));
			Assert.True(CompanionService.TryParseClass("Spirit Master", out PlayerClass spoken) && spoken == PlayerClass.SPIRIT_MASTER);
			Assert.True(CompanionService.TryParseClass("SONGWEAVER", out spoken) && spoken == PlayerClass.BARD);
			Assert.Empty(service.All());

			session.BeginStep("s03", "made-from-its-profile");
			service.OfferSeat(seats[0], $"sim-player-{seats[0]}");
			Assert.Contains(await SayAsync(".companion Cleric"), message => message.Contains("ClericA the cleric is on its way to you.", StringComparison.Ordinal));
			CompanionInfo made = Assert.Single(service.Of(leader));
			Assert.Equal((seats[0], "ClericA", PlayerClass.CLERIC, CompanionState.Requested), (made.AccountId, made.Name, made.PlayerClass, made.State));
			Assert.Contains(await SayAsync(".companion templar"), message => message.Contains("No companion is free right now.", StringComparison.Ordinal));
			Assert.Contains(await SayAsync(".companion list"), message => message.Contains("ClericA, cleric", StringComparison.Ordinal));
			Assert.Equal([made.CharacterId], PlayerDAO.GetPlayerOidsOnAccount(seats[0]));

			session.BeginStep("s04", "a-host-plays-it-as-a-client");
			CompanionRequest request = service.TakeRequest(seats[0]) ?? throw new InvalidDataException("The seat's request was not there to take.");
			Assert.Null(service.TakeRequest(seats[0]));
			Assert.Equal((made.CharacterId, "ClericA", session.CharacterId, "Asimleader"), (request.CharacterId, request.Name, request.LeaderId, request.LeaderName));
			CompanionProfile profile = CompanionProfile.LoadAll(Path.Combine(Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "game-server", "data", "companions"))
				.Single(candidate => candidate.Race == Race.ASMODIANS && candidate.PlayerClass == PlayerClass.CLERIC && candidate.Level == request.Level);
			await using (var played = new SimulationL0Session(fixture, policy, "b02", seats[0], request.Name, Race.ASMODIANS))
			{
				var list = await played.LoginCharacterListAsync(token);
				var listed = Assert.Single(list.Get<List<IReadOnlyDictionary<string, object?>>>("characters"));
				Assert.Equal((request.CharacterId, request.Name, (int)PlayerClass.CLERIC), (Get<int>(listed, "objectId"), Get<string>(listed, "name"), Get<int>(listed, "playerClass")));
				played.SelectCharacter(request.CharacterId, request.Name);
				await played.EnterWorldAsync(token);
				await played.SynchronizeAsync(token);
				Player companion = fixture.World.GetPlayer(request.CharacterId);
				Assert.Equal((PlayerClass.CLERIC, 26, true), (companion.GetPlayerClass(), (int)companion.GetLevel(), companion.GetCommonData().IsDaeva()));
				Assert.Equal(leader.GetWorldId(), companion.GetWorldId());
				Assert.InRange(Math.Sqrt(Math.Pow(companion.GetX() - leader.GetX(), 2) + Math.Pow(companion.GetY() - leader.GetY(), 2)), 0, 1);
				Assert.Equal((220010000, 571f, 2787f, 299f), (companion.GetBindPoint().GetMapId(), companion.GetBindPoint().GetX(), companion.GetBindPoint().GetY(), companion.GetBindPoint().GetZ()));
				Assert.Equal(profile.Equipment.Select(worn => (worn.ItemId, worn.Slot, worn.Count)).Order(),
					companion.GetEquipment().GetEquippedItems().Select(item => (item.GetItemId(), item.GetEquipmentSlot(), item.GetItemCount())).Distinct().Order());
				Assert.Equal(profile.Inventory.Select(carried => (carried.ItemId, carried.Count)).Order(),
					companion.GetInventory().GetItems().Select(item => (item.GetItemId(), item.GetItemCount())).Order());
				Assert.Equal(profile.Skills.Select(skill => (skill.Id, skill.Level)).Order(),
					companion.GetSkillList().GetAllSkills().Where(skill => !skill.IsStigmaSkill()).Select(skill => (skill.GetSkillId(), skill.GetSkillLevel())).Order());
				// Its stigma: the stone is one of the worn items above, and stays in its slot at the login only because the
				// companion was made with the stigma quest done. The login teaches the stone's skills (Saving Grace I and II).
				Assert.Contains(profile.Equipment, worn => worn.Slot == (long)Aion.GameServer.Model.Items.ItemSlot.STIGMA1 && worn.ItemId == 140001431);
				Assert.Equal(Aion.GameServer.QuestEngine.Model.QuestStatus.COMPLETE, companion.GetQuestStateList().GetQuestState(2900)?.GetStatus());
				Assert.Equal(new[] { 3924, 3925 },
					companion.GetSkillList().GetAllSkills().Where(skill => skill.IsStigmaSkill()).Select(skill => skill.GetSkillId()).Order());
				Assert.True(service.IsCompanion(request.CharacterId));
				// It is in its player's group from the moment it enters the world, and the player leads.
				PlayerGroup group = leader.GetPlayerGroup();
				Assert.NotNull(group);
				Assert.Same(group, companion.GetPlayerGroup());
				Assert.True(group.IsLeader(leader));
				Assert.Equal(new[] { session.CharacterId, request.CharacterId }.Order(), group.GetMembers().Select(member => member.GetObjectId()).Order());
				await session.SynchronizeAsync(token);
				Assert.Equal(session.CharacterId, session.Api.World.GroupLeaderId);
				Assert.Contains(request.CharacterId, session.Api.World.GroupMembers.Keys);
				// C6: with its first companion the group's loot is free for all, every quality picked up as it is.
				Assert.Equal(LootRuleType.FREEFORALL, group.GetLootGroupRules().GetLootRule());
				Assert.Equal(0, group.GetLootGroupRules().GetAutodistributionId());
				Assert.Equal(new[] { 0, 0, 0, 0, 0, 0, 0, 0 }, session.Api.World.GroupLootRules);

				session.BeginStep("s05", "who-may-ask");
				Assert.Equal("A companion cannot bring companions.", service.Request(companion, "cleric"));
				PlayerGroupService.ChangeLeader(companion);
				Assert.Equal("Only the leader of your group can bring a companion.", service.Request(leader, "templar"));
				PlayerGroupService.ChangeLeader(leader);
				Assert.True(group.IsLeader(leader));

				session.BeginStep("s06", "sent-away-and-deleted-when-it-has-left");
				Assert.Contains(await SayAsync(".companion dismiss"), message => message.Contains("ClericA leaves you.", StringComparison.Ordinal));
				Assert.Equal(CompanionState.Ending, Assert.Single(service.Of(leader)).State);
				Assert.False(leader.IsInGroup());
				Assert.False(companion.IsInGroup());
				Assert.Equal(1, Rows("players", "id", request.CharacterId));
				await played.QuitAsync(token);
			}
			Assert.Empty(service.All());
			AssertGone(request.CharacterId);
			Assert.Empty(PlayerDAO.GetPlayerOidsOnAccount(seats[0]));

			session.BeginStep("s07", "a-group-holds-six");
			foreach (int seat in seats.Skip(1))
				service.OfferSeat(seat, $"sim-player-{seat}");
			foreach (string playerClass in new[] { "cleric", "templar", "sorcerer", "gladiator", "ranger" })
				Assert.Contains(await SayAsync($".companion {playerClass}"), message => message.Contains("is on its way to you.", StringComparison.Ordinal));
			Assert.Contains(await SayAsync(".companion chanter"), message => message.Contains("Your group is full.", StringComparison.Ordinal));
			int[] five = service.Of(leader).Select(companion => companion.CharacterId).ToArray();
			Assert.Equal(5, five.Length);
			Assert.Equal(5, service.Of(leader).Select(companion => companion.AccountId).Distinct().Count());

			session.BeginStep("s08", "one-nobody-plays-is-deleted-at-once");
			string sorcerer = service.Of(leader).Single(companion => companion.PlayerClass == PlayerClass.SORCERER).Name;
			Assert.Contains(await SayAsync($".companion dismiss {sorcerer.ToLowerInvariant()}"), message => message.Contains($"{sorcerer} leaves you.", StringComparison.Ordinal));
			Assert.Equal(4, service.Of(leader).Count);
			AssertGone(five[2]);
			Assert.Contains(await SayAsync(".companion dismiss nobody"), message => message.Contains("You have no companion called nobody.", StringComparison.Ordinal));

			session.BeginStep("s08b", "the-server-ends-a-companion-whose-player-left-the-group");
			// A second client logs the Templar in and then does nothing by itself: a host that hangs.
			CompanionInfo templar = service.Of(leader).Single(companion => companion.PlayerClass == PlayerClass.TEMPLAR);
			CompanionRequest second = service.TakeRequest(templar.AccountId) ?? throw new InvalidDataException("The Templar's request was not there to take.");
			await using (var idle = new SimulationL0Session(fixture, policy, "b03", second.AccountId, second.Name, Race.ASMODIANS))
			{
				await idle.LoginCharacterListAsync(token);
				idle.SelectCharacter(second.CharacterId, second.Name);
				await idle.EnterWorldAsync(token);
				await idle.SynchronizeAsync(token);
				await session.SynchronizeAsync(token);
				Assert.True(leader.GetPlayerGroup().IsLeader(leader));
				Assert.Equal(CompanionState.Playing, service.Of(leader).Single(companion => companion.CharacterId == second.CharacterId).State);
				// The player leaves its group, as a client does. Nobody leads the companion any more.
				await session.SendPacketAsync(session.Api.LeaveGroup(), token);
				await session.SynchronizeAsync(token);
				Assert.False(leader.IsInGroup());
				// The server's next look ends it,
				await session.AdvanceAsync(TimeSpan.FromSeconds(CompanionService.WatchSeconds + 1), token);
				Assert.Equal(CompanionState.Ending, service.Of(leader).Single(companion => companion.CharacterId == second.CharacterId).State);
				Assert.NotNull(fixture.World.GetPlayer(second.CharacterId));
				// and one that stays is disconnected, and deleted when it has left the world.
				await session.AdvanceAsync(TimeSpan.FromSeconds(CompanionService.LingerSeconds + CompanionService.WatchSeconds + 12), token);
				Assert.Null(fixture.World.GetPlayer(second.CharacterId));
				Assert.DoesNotContain(service.All(), companion => companion.CharacterId == second.CharacterId);
			}
			AssertGone(second.CharacterId);

			session.BeginStep("s08c", "one-nobody-logs-in-is-deleted-in-time");
			Assert.Equal(3, service.Of(leader).Count);
			await session.AdvanceAsync(TimeSpan.FromSeconds(CompanionService.UnplayedSeconds), token);
			Assert.Empty(service.All());
			foreach (int characterId in five)
				AssertGone(characterId);

			session.BeginStep("s09", "the-players-logout-ends-them");
			Assert.Contains(await SayAsync(".companion chanter"), message => message.Contains("is on its way to you.", StringComparison.Ordinal));
			int last = Assert.Single(service.Of(leader)).CharacterId;
			Assert.Equal(1, Rows("players", "id", last));
			await session.QuitAsync(token);
			AssertGone(last);
			Assert.Empty(service.All());
			foreach (int characterId in five)
				AssertGone(characterId);
		}
		finally
		{
			service.Disable();
			foreach (int seat in seats)
				service.WithdrawSeat(seat);
		}
		policy.AssertClean();
	}

	private static long CompanionRows(string table, string column, int id)
	{
		using var connection = DatabaseFactory.GetConnection(); connection.Open();
		using var command = connection.CreateCommand();
		command.CommandText = $"SELECT COUNT(*) FROM {table} WHERE {column}=@id";
		command.Parameters.AddWithValue("@id", id);
		return Convert.ToInt64(command.ExecuteScalar());
	}

	private static void AssertCompanionGone(int characterId)
	{
		foreach ((string table, string column) in new[] { ("players", "id"), ("player_appearance", "player_id"), ("player_skills", "player_id"),
			("inventory", "item_owner"), ("player_quests", "player_id"), ("player_bind_point", "player_id") })
			Assert.True(CompanionRows(table, column, characterId) == 0, $"{table} still has rows of the deleted companion {characterId}.");
	}

	/// <summary>
	/// D41 (docs/companion-live-host.md, L1): a seat is an account named to the server that waits at character selection.
	/// A host needs nothing but the client protocol. The companion a player asks for appears in the seat's character list;
	/// the seat enters the world with it, and back at character selection after its end the list is empty and the seat
	/// waits again. A companion's character has the note that marks it. A seat that logs in with marked characters, as
	/// after a server that stopped with companions alive, has them deleted. An account named as a seat that holds any
	/// other character is no seat, and its character is not touched. A seat whose client goes away takes a companion
	/// that nobody played with it.
	/// </summary>
	[SkippableFact]
	public async Task CompanionSeatIsAnAccountWaitingAtCharacterSelection()
	{
		SkipUnlessCompanionServerTests();
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		using var policy = NewPolicy("D41-seat", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		CancellationToken token = timeout.Token;
		CompanionService service = CompanionService.GetInstance();
		const int leaderAccount = 110, seatAccount = 108, playerAccount = 109;
		string seatName = $"sim-player-{seatAccount}";
		await using var session = new SimulationL0Session(fixture, policy, "b01", leaderAccount, "Asimseatlead", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player leader = fixture.World.GetPlayer(session.CharacterId);

		async Task<string[]> SayAsync(string text)
		{
			int start = session.PacketHistory.Count;
			await session.SendPacketAsync(session.Api.Say(text), token);
			await session.SynchronizeAsync(token);
			return session.PacketHistory.Skip(start).Where(packet => packet.PacketType == typeof(SM_MESSAGE))
				.Select(packet => packet.Get<string>("message")).ToArray();
		}
		static List<IReadOnlyDictionary<string, object?>> Characters(Aion.Bots.Protocol.DecodedBotServerPacket list) =>
			list.Get<List<IReadOnlyDictionary<string, object?>>>("characters");

		try
		{
			session.BeginStep("s01", "no-seat-is-logged-in");
			// The seat is named by a pattern, as the live host's are ("sim-player-10*" is sim-player-10 and digits); the
			// player's account that was named by mistake is named outright.
			service.Enable(seatAccounts: ["sim-player-10*", $"sim-player-{playerAccount}"]);
			Assert.Empty(service.WaitingSeats());
			Assert.Contains(await SayAsync(".companion cleric"), message => message.Contains("No companion is free right now.", StringComparison.Ordinal));

			session.BeginStep("s02", "a-seat-waits-at-character-selection");
			await using var seat = new SimulationL0Session(fixture, policy, "b02", seatAccount, "Unused", Race.ASMODIANS);
			Assert.Empty(Characters(await seat.LoginCharacterListAsync(token)));
			Assert.Equal([seatName], service.WaitingSeats());

			session.BeginStep("s03", "the-companion-appears-in-the-seats-list");
			Assert.Contains(await SayAsync(".companion cleric"), message => message.Contains("the cleric is on its way to you.", StringComparison.Ordinal));
			CompanionInfo made = Assert.Single(service.Of(leader));
			Assert.Equal((seatAccount, PlayerClass.CLERIC, CompanionState.Requested), (made.AccountId, made.PlayerClass, made.State));
			Assert.Empty(service.WaitingSeats());
			Assert.Equal(CompanionService.Note, PlayerDAO.LoadPlayerCommonData(made.CharacterId).GetNote());
			var listed = Assert.Single(Characters(await seat.ReadCharacterListAsync(token)));
			Assert.Equal((made.CharacterId, made.Name, (int)PlayerClass.CLERIC), (Get<int>(listed, "objectId"), Get<string>(listed, "name"), Get<int>(listed, "playerClass")));
			seat.SelectCharacter(made.CharacterId, made.Name);
			await seat.EnterWorldAsync(token);
			await seat.SynchronizeAsync(token);
			await session.SynchronizeAsync(token);
			Player companion = fixture.World.GetPlayer(made.CharacterId);
			Assert.Equal((PlayerClass.CLERIC, 26), (companion.GetPlayerClass(), (int)companion.GetLevel()));
			Assert.True(leader.GetPlayerGroup().IsLeader(leader));
			Assert.Same(leader.GetPlayerGroup(), companion.GetPlayerGroup());
			Assert.Equal(CompanionState.Playing, Assert.Single(service.Of(leader)).State);

			session.BeginStep("s03b", "the-two-words-of-a-companion");
			// A player is refused both: they are a companion's own.
			Assert.Contains(await SayAsync(".companion come"), message => message.Contains("Only a companion", StringComparison.Ordinal));
			Assert.Contains(await SayAsync(".companion restock"), message => message.Contains("Only a companion", StringComparison.Ordinal));
			Assert.Contains("Your supplies are complete.", service.Restock(companion));
			// What it has used is given back, up to what its profile gave it, and not twice in five minutes.
			var used = companion.GetInventory().GetItems().First(item => item.GetItemCount() > 5 && !item.IsEquipped());
			long had = companion.GetInventory().GetItemCountByItemId(used.GetItemId());
			Assert.Contains("a moment ago", service.Restock(companion));
			Assert.True(companion.GetInventory().DecreaseByItemId(used.GetItemId(), 3, companion));
			Assert.Equal(had - 3, companion.GetInventory().GetItemCountByItemId(used.GetItemId()));
			fixture.Clock.Advance(TimeSpan.FromSeconds(CompanionService.RestockSeconds + 1));
			Assert.Contains($"{used.GetItemId()}x3", service.Restock(companion));
			Assert.Equal(had, companion.GetInventory().GetItemCountByItemId(used.GetItemId()));
			await seat.SynchronizeAsync(token);
			await session.SynchronizeAsync(token);

			session.BeginStep("s04", "back-at-character-selection-the-seat-waits-again");
			Assert.Contains(await SayAsync(".companion dismiss"), message => message.Contains("leaves you.", StringComparison.Ordinal));
			await seat.ReturnToSelectionAsync(editing: false, token);
			Assert.Empty(service.All());
			AssertCompanionGone(made.CharacterId);
			Assert.Empty(Characters(await seat.ReadCharacterListAsync(token)));
			Assert.Equal([seatName], service.WaitingSeats());

			session.BeginStep("s05", "what-a-stopped-server-left-is-deleted-at-the-seats-next-login");
			Assert.Contains(await SayAsync(".companion templar"), message => message.Contains("is on its way to you.", StringComparison.Ordinal));
			int left = Assert.Single(service.Of(leader)).CharacterId;
			await seat.CloseSelectionAsync(token);
			service.ForgetForTests();
			Assert.Equal(1, CompanionRows("players", "id", left));
			await using var again = new SimulationL0Session(fixture, policy, "b03", seatAccount, "Unused", Race.ASMODIANS);
			Assert.Equal(left, Get<int>(Assert.Single(Characters(await again.LoginCharacterListAsync(token))), "objectId"));
			Assert.Equal([seatName], service.WaitingSeats());
			AssertCompanionGone(left);
			Assert.Empty(Characters(await again.ReadCharacterListAsync(token)));

			session.BeginStep("s06", "an-account-with-a-players-character-is-no-seat");
			await using var mistaken = new SimulationL0Session(fixture, policy, "b04", playerAccount, "Asimnotaseat", Race.ASMODIANS);
			await mistaken.LoginAndAuthenticateAsync(token);
			await mistaken.CreateCharacterAsync(token, PlayerClass.PRIEST);
			Assert.Equal([seatName], service.WaitingSeats());
			Assert.Contains(await SayAsync(".companion cleric"), message => message.Contains("is on its way to you.", StringComparison.Ordinal));
			Assert.Contains(await SayAsync(".companion templar"), message => message.Contains("No companion is free right now.", StringComparison.Ordinal));
			Assert.Equal(1, CompanionRows("players", "id", mistaken.CharacterId));
			Assert.Single(Characters(await mistaken.ReadCharacterListAsync(token)));

			session.BeginStep("s07", "a-seat-that-goes-away-takes-its-unplayed-companion-with-it");
			int unplayed = Assert.Single(service.Of(leader)).CharacterId;
			await again.CloseSelectionAsync(token);
			Assert.Empty(service.WaitingSeats());
			Assert.Empty(service.All());
			AssertCompanionGone(unplayed);
			Assert.Equal(1, CompanionRows("players", "id", mistaken.CharacterId));
		}
		finally
		{
			service.Disable();
			service.ForgetForTests();
		}
		policy.AssertClean();
	}
}
