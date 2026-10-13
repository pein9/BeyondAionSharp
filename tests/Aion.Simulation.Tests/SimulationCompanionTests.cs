using Aion.Commons.Database;
using Aion.GameServer.Custom.Companion;
using Aion.GameServer.Dao;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// D41 (docs/companion-bots.md, C2): the server's part of the companion bots, which Java does not have. While the
	/// feature is off <c>.companion</c> is no command. Switched on, it makes a level-26 character of the class from its
	/// generated profile where the player stands, on an account a host offered, and leaves a request for that host. The
	/// character logs in as any other and is in the player's group, which the player leads, from the moment it enters the
	/// world (C3). One that is sent away, or whose player logs out, is deleted with its rows when it
	/// has left the world; one that nobody came to play is deleted at once.
	/// </summary>
	[SkippableFact]
	public async Task CompanionCommandMakesAThrowawayCharacterAndDeletesIt()
	{
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
				.Single(candidate => candidate.Race == Race.ASMODIANS && candidate.PlayerClass == PlayerClass.CLERIC);
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
					companion.GetSkillList().GetAllSkills().Select(skill => (skill.GetSkillId(), skill.GetSkillLevel())).Order());
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

			session.BeginStep("s09", "the-players-logout-ends-them");
			await session.QuitAsync(token);
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
}
