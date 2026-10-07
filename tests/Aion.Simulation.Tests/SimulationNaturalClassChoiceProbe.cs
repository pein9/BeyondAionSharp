using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// CP-31: the two SIM accounts of the class-profile probes (docs/natural-class-profiles.md). Every other id the fixture
	/// accepts is in use, and a SIM account must be fresh, so a probe plays at most two rows in one process; more rows
	/// are run in further processes, each on its own schema.
	/// </summary>
	private const int ProbeAccountA = 98, ProbeAccountB = 100;

	/// <param name="CeremonyItemId">The weapon taken at the ceremony; null for the reviewed bridge's pick.</param>
	private sealed record ClassChoiceRow(string Name, PlayerClass Starter, PlayerClass Second, int Account, string CharacterName, int? CeremonyItemId);

	private static readonly ClassChoiceRow[] ClassChoiceRows =
	[
		new("cleric", PlayerClass.PRIEST, PlayerClass.CLERIC, ProbeAccountA, "Asimpickcleric", null),
		// CP-Q7, on its default: the Chanter takes the Karmic Staff.
		new("chanter", PlayerClass.PRIEST, PlayerClass.CHANTER, ProbeAccountB, "Asimpickchanter", 101500498),
	];

	public static TheoryData<string> ClassChoiceRowNames => new(ClassChoiceRows.Select(row => row.Name));

	/// <summary>A gated probe row runs only when CP_PROBE_ROWS names it, in a comma-separated list.</summary>
	private static bool ProbeRowNamed(string row) => (Environment.GetEnvironmentVariable("CP_PROBE_ROWS") ?? "")
		.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(row);

	/// <summary>
	/// CP-31: the class choice on a prepared character, through the capital scenario as CP-30 parameterized it and up to
	/// its short endpoint. Prepared here, and nowhere in a journey: the character is created by packets as the row's
	/// starter, then set to level 9 and placed by Munin by the test (the scenario's own setup step). From there every
	/// step is a client action: Q2008 with the row's class choice, the Q2009 ceremony with the row's weapon, and Doman's
	/// SETPRO1 on the dispatch quest.
	/// </summary>
	[SkippableTheory]
	[MemberData(nameof(ClassChoiceRowNames))]
	public async Task NaturalClassChoiceProbe(string rowName)
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		Skip.IfNot(ProbeRowNamed(rowName), $"Row {rowName} is not named in CP_PROBE_ROWS.");
		ClassChoiceRow row = ClassChoiceRows.Single(candidate => candidate.Name == rowName);
		NaturalAscensionContract reviewed = NaturalAscensionContract.LoadDefault();
		NaturalAscensionContract contract = NaturalAscensionContract.ForChoice(reviewed, NaturalClassLineContract.LoadDefault(),
			row.Starter, row.Second, row.CeremonyItemId);
		string id = "CP31-" + row.Name;
		using var policy = NewEconomyPolicy(id, includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
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
		float[] start = contract.Steps.First(step => step.NpcId == contract.Start.NpcId).Position;
		var munin = new BotPosition(start[0], start[1], start[2], 0);
		NaturalAscensionStep ceremony = contract.Step(NaturalAscensionStepRole.Ceremony);
		var driver = new SimCapitalDriver(fixture, session, player, id,
			token => SetLevelNineAtAsync(session, player, contract.Start.MapId, munin, token),
			() =>
			{
				// The short endpoint: in Pandaemonium, the dispatch quest moved to var 1 by Doman and not turned in.
				Assert.Equal(ceremony.MapId, player.GetWorldId());
				Assert.Equal(row.Second, player.GetPlayerClass());
				Assert.Equal(contract.Endpoint.ClassId, (int)player.GetPlayerClass().GetClassId());
				Assert.Equal((byte)contract.Endpoint.ClassId, session.Api.World.Objects[session.CharacterId].PlayerClass);
				Assert.True(player.GetCommonData().IsDaeva());
				Assert.True(player.GetLevel() >= contract.Endpoint.MinimumLevel);
				// The second class's level-9 masteries, on the server and in the skill list the client was sent.
				Assert.Equal(6, contract.ClassChoice.MasterySkillIds.Length);
				Assert.All(contract.ClassChoice.MasterySkillIds, mastery =>
				{
					Assert.True(player.GetSkillList().IsSkillPresent(mastery), $"{row.Second} has no mastery {mastery}.");
					Assert.True(session.Api.World.Skills.ContainsKey(mastery), $"The client was not sent mastery {mastery}.");
				});
				foreach (int quest in new[] { contract.ClassChoice.QuestId, contract.CeremonyReward.QuestId })
					Assert.Equal(QuestStatus.COMPLETE, player.GetQuestStateList().GetQuestState(quest).GetStatus());
				// The ceremony paid the row's weapon, and the server's reward list for this class at Q2009 holds it at the
				// place the reward action named.
				Assert.NotNull(player.GetInventory().GetFirstItemByItemId(contract.CeremonyReward.ItemId));
				List<QuestItems> list = DataManager.QUEST_DATA.GetQuestById(contract.CeremonyReward.QuestId).GetSelectableRewardByClass(row.Second);
				int place = NaturalAscensionContract.DialogActionId(contract.CeremonyReward.Action) - DialogAction.SELECTED_QUEST_REWARD1;
				Assert.Equal(contract.CeremonyReward.ItemId, list[place].GetItemId());
				Assert.Equal(contract.CeremonyReward.RewardGroup, player.GetQuestStateList().GetQuestState(contract.CeremonyReward.QuestId).GetRewardGroup());
				// The dispatch quest started with the ceremony (the scenario saw START/0) and Doman moved it to var 1.
				QuestState dispatch = player.GetQuestStateList().GetQuestState(contract.Dispatch.QuestId);
				Assert.Equal((QuestStatus.START, 1), (dispatch.GetStatus(), dispatch.GetQuestVarById(0)));
				Assert.Equal(contract.Dispatch.StartReward, contract.CeremonyReward.RewardGroup);
				// Every quest movie was played and answered.
				Assert.Equal(contract.Movies.Order(), session.PacketHistory.Where(packet => packet.PacketType == typeof(SM_PLAY_MOVIE))
					.Select(packet => packet.Get<int>("cutsceneId")).Where(contract.Movies.Contains).Order());
				Assert.False(player.IsInCustomState(CustomPlayerState.WATCHING_CUTSCENE));
				int[] starterMasteries = NaturalClassLineContract.LoadDefault().Starter(row.Starter).Masteries.Select(mastery => mastery.SkillId).ToArray();
				Console.WriteLine($"{id}: class {player.GetPlayerClass()} ({(int)player.GetPlayerClass().GetClassId()}), level {player.GetLevel()}, " +
					$"action {contract.ClassChoice.Action}, masteries {string.Join(' ', contract.ClassChoice.MasterySkillIds)}, " +
					$"starter masteries still held {string.Join(' ', starterMasteries.Where(mastery => player.GetSkillList().IsSkillPresent(mastery)))}, " +
					$"ceremony item {contract.CeremonyReward.ItemId} by {contract.CeremonyReward.Action} from {contract.CeremonyReward.SelectableList}, " +
					$"Q{contract.Dispatch.QuestId} {dispatch.GetStatus()}/{dispatch.GetQuestVarById(0)}.");
			});
		await CapitalAscensionScenario.RunAsmodianAsync(driver, contract, token, stopAtDispatchStart: true);
		policy.AssertClean();
	}
}
