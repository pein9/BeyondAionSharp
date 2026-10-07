using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	// CP-34: six rows on the two probe accounts, so they run two to a process: warrior,scout / mage,priest / engineer,artist.
	private static readonly (string Name, PlayerClass Starter, int Account, string CharacterName)[] NewSkillTrainerRows =
	[
		("warrior", PlayerClass.WARRIOR, ProbeAccountA, "Asimtrwarrior"),
		("scout", PlayerClass.SCOUT, ProbeAccountB, "Asimtrscout"),
		("mage", PlayerClass.MAGE, ProbeAccountA, "Asimtrmage"),
		("priest", PlayerClass.PRIEST, ProbeAccountB, "Asimtrpriest"),
		("engineer", PlayerClass.ENGINEER, ProbeAccountA, "Asimtrengineer"),
		("artist", PlayerClass.ARTIST, ProbeAccountB, "Asimtrartist"),
	];

	public static TheoryData<string> NewSkillTrainerRowNames => new(NewSkillTrainerRows.Select(row => row.Name));

	/// <summary>
	/// CP-34: Q2132 "A New Skill" at the six class trainers (Java _2132ANewSkill). Prepared here, and nowhere in a
	/// journey: the character is created by packets as the row's starter, then the test sets it to level 3, which starts
	/// Q2132 in REWARD with the class's var, and places it by a trainer. From there the client talks: another class's
	/// trainer offers it no Q2132 page, and its own trainer shows the class's page and pays.
	/// </summary>
	[SkippableTheory]
	[MemberData(nameof(NewSkillTrainerRowNames))]
	public async Task NaturalNewSkillTrainerProbe(string rowName)
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		Skip.IfNot(ProbeRowNamed(rowName), $"Row {rowName} is not named in CP_PROBE_ROWS.");
		var row = NewSkillTrainerRows.Single(candidate => candidate.Name == rowName);
		NaturalClassLineContract lines = NaturalClassLineContract.LoadDefault();
		NaturalStarterClass own = lines.Starter(row.Starter);
		NaturalStarterClass other = lines.Starters[(Array.IndexOf(lines.Starters, own) + 1) % lines.Starters.Length];
		int quest = lines.NewSkillQuestId;
		const int ishalgen = 220010000;
		string id = "CP34-" + row.Name;
		using var policy = NewEconomyPolicy(id, includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		var token = timeout.Token;
		await using var session = new SimulationL0Session(fixture, policy, "b01", row.Account, row.CharacterName, Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, row.Starter);
		await session.EnterWorldAsync(token);
		await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
		Assert.Equal(row.Starter, player.GetPlayerClass());

		// Prepared by the test: level 3, and a place two meters from the other class's trainer.
		session.BeginStep("s01", "setup-level-three-by-another-class-trainer");
		player.GetCommonData().SetLevel(3);
		await TeleportForSetupAsync(session, player, ishalgen, other.NewSkill.TrainerPosition[0] - 2, other.NewSkill.TrainerPosition[1],
			other.NewSkill.TrainerPosition[2], token);
		await session.SynchronizeAsync(token);
		Assert.True(session.Api.World.Quests.TryGetValue(quest, out var started), $"Q{quest} did not start at level 3.");
		Assert.Equal(((byte)4, own.NewSkill.Var), (started!.Status, started.StepAndFlags));
		Assert.Equal(own.NewSkill.RewardGroup, player.GetQuestStateList().GetQuestState(quest).GetRewardGroup());

		// The trainer the client sees nearest the contract's position. The client may see the template more than once; the
		// row prints every one it saw.
		var seen = new List<string>();
		int Trainer(NaturalStarterClass starter)
		{
			var matches = session.Api.World.Objects.Values.Where(npc => npc.TemplateId == starter.NewSkill.TrainerNpcId)
				.OrderBy(npc => MathF.Abs(npc.Position.X - starter.NewSkill.TrainerPosition[0]) + MathF.Abs(npc.Position.Y - starter.NewSkill.TrainerPosition[1]))
				.ToArray();
			Assert.NotEmpty(matches);
			seen.Add($"{starter.NewSkill.TrainerNpcId}: " + string.Join(", ", matches.Select(npc =>
				$"{npc.Kind} object {npc.ObjectId} at ({npc.Position.X:F1},{npc.Position.Y:F1},{npc.Position.Z:F1}), on the server a " +
				$"{fixture.World.FindVisibleObject(npc.ObjectId)?.GetType().Name ?? "missing object"}")));
			return matches[0].ObjectId;
		}

		// Another class's trainer: the dialog that opens is not Q2132's, and the quest stays where it was.
		session.BeginStep("s02", "another-class-trainer-offers-nothing");
		int stranger = Trainer(other);
		await NaturalDialogProtocol.OpenAsync(session, stranger, token);
		var refused = await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == stranger);
		int refusedQuest = refused.Get<int>("questId"), refusedPage = refused.Get<ushort>("dialogPageId");
		Assert.NotEqual(quest, refusedQuest);
		Assert.DoesNotContain(refusedPage, lines.Starters.Select(starter => starter.NewSkill.PageId));
		await session.SendPacketAsync(session.Api.CloseDialog(stranger), token);
		await session.SynchronizeAsync(token);
		Assert.Equal(((byte)4, own.NewSkill.Var), (session.Api.World.Quests[quest].Status, session.Api.World.Quests[quest].StepAndFlags));
		Assert.DoesNotContain(quest, session.Api.World.CompletedQuestIds);

		// Its own trainer: the class's page on the talk, then the turn-in the journey sends.
		session.BeginStep("s03", "own-trainer-pays");
		await TeleportForSetupAsync(session, player, ishalgen, own.NewSkill.TrainerPosition[0] - 2, own.NewSkill.TrainerPosition[1],
			own.NewSkill.TrainerPosition[2], token);
		await session.SynchronizeAsync(token);
		int trainer = Trainer(own);
		long experienceBefore = player.GetCommonData().GetExp();
		await NaturalDialogProtocol.OpenAsync(session, trainer, token);
		var page = await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == trainer);
		Assert.Equal((quest, own.NewSkill.PageId), (page.Get<int>("questId"), (int)page.Get<ushort>("dialogPageId")));
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(trainer, DialogAction.QUEST_SELECT, questId: quest), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == trainer);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(trainer, DialogAction.SELECTED_QUEST_NOREWARD, questId: quest), token);
		for (int wait = 0; wait < 10 && !session.Api.World.CompletedQuestIds.Contains(quest); wait++)
			await session.SynchronizeAsync(token);
		await session.SendPacketAsync(session.Api.CloseDialog(trainer), token);
		await session.SynchronizeAsync(token);
		Assert.Contains(quest, session.Api.World.CompletedQuestIds);
		Assert.Equal(QuestStatus.COMPLETE, player.GetQuestStateList().GetQuestState(quest).GetStatus());
		Assert.False(player.IsDead());
		Console.WriteLine($"{id}: {row.Starter} at level {player.GetLevel()}, Q{quest} REWARD var {own.NewSkill.Var} group {own.NewSkill.RewardGroup}; " +
			$"trainer {other.NewSkill.TrainerNpcId} of {other.Class} opened page {refusedPage} for quest {refusedQuest}; " +
			$"own trainer {own.NewSkill.TrainerNpcId} opened page {own.NewSkill.PageId} and paid {player.GetCommonData().GetExp() - experienceBefore} experience. " +
			$"Seen: {string.Join("; ", seen)}.");
		policy.AssertClean();
	}
}
