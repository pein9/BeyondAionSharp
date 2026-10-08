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
	/// <param name="Masteries">How many masteries the second class is given at level 9, as read from Java's skill tree.</param>
	/// <param name="Item">The checklist item the row belongs to; it names the row's run.</param>
	/// <param name="TrialSwings">The normal attacks Hellion is given; null for the scenario's own bound of 600.</param>
	private sealed record ClassChoiceRow(string Name, PlayerClass Starter, PlayerClass Second, int Account, string CharacterName, int? CeremonyItemId,
		int Masteries = 6, string Item = "CP31", int? TrialSwings = null);

	/// <summary>
	/// CP-67a: the Scout's bound for Hellion. The Training Dagger does 15 to 17 at the Scout's power of 100, and Hellion's
	/// physical defence of 184 takes 18.4 off a swing (Java AttackUtil.adjustDamageByStatModifiers, with the defence from
	/// NpcStatCalculation: 9 x 17 x 1.2), so every swing does the minimum of 1, as in Java. Measured in CP-67: 600 swings
	/// took 38% of his 1,461 HP, which is about 1,570 swings for all of it.
	/// </summary>
	private const int ScoutTrialSwings = 2000;

	private static readonly ClassChoiceRow[] ClassChoiceRows =
	[
		new("cleric", PlayerClass.PRIEST, PlayerClass.CLERIC, ProbeAccountA, "Asimpickcleric", null),
		// CP-Q7, on its default: the Chanter takes the Karmic Staff.
		new("chanter", PlayerClass.PRIEST, PlayerClass.CHANTER, ProbeAccountB, "Asimpickchanter", 101500498),
		// CP-67: the other nine second classes, two rows to a process. The weapon each takes is this probe's pick from
		// the class's own list, not an operator decision: the weapon of the class type where the gear rules name one
		// (greatsword, sword, spellbook), the dagger and the bow for the two Scout classes, and the only weapon offered
		// for the Gunner, the Bard and the Rider.
		new("gladiator", PlayerClass.WARRIOR, PlayerClass.GLADIATOR, ProbeAccountA, "Asimpickglad", 100900488, 10, "CP67"),
		new("templar", PlayerClass.WARRIOR, PlayerClass.TEMPLAR, ProbeAccountB, "Asimpicktemplar", 100000640, 7, "CP67"),
		new("assassin", PlayerClass.SCOUT, PlayerClass.ASSASSIN, ProbeAccountA, "Asimpicksin", 100200605, 4, "CP67", ScoutTrialSwings),
		new("ranger", PlayerClass.SCOUT, PlayerClass.RANGER, ProbeAccountB, "Asimpickranger", 101700515, 4, "CP67", ScoutTrialSwings),
		new("sorcerer", PlayerClass.MAGE, PlayerClass.SORCERER, ProbeAccountA, "Asimpicksorc", 100600532, 3, "CP67"),
		new("spiritmaster", PlayerClass.MAGE, PlayerClass.SPIRIT_MASTER, ProbeAccountB, "Asimpickspirit", 100600532, 3, "CP67"),
		new("gunner", PlayerClass.ENGINEER, PlayerClass.GUNNER, ProbeAccountA, "Asimpickgunner", 101800506, 1, "CP67"),
		new("bard", PlayerClass.ARTIST, PlayerClass.BARD, ProbeAccountB, "Asimpickbard", 102000523, 1, "CP67"),
		new("rider", PlayerClass.ENGINEER, PlayerClass.RIDER, ProbeAccountA, "Asimpickrider", 102100489, 2, "CP67"),
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
	/// <para>
	/// CP-67: a row of another starter holds that starter's first weapon and fights the trial with it, inside the
	/// scenario's own swing bound.
	/// </para>
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
		string id = $"{row.Item}-{row.Name}";
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
				Assert.Equal(row.Masteries, contract.ClassChoice.MasterySkillIds.Length);
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
				// The attacks the server carried out for this character, by opponent: the four guardian assassins and Hellion.
				int[] swings = session.PacketHistory.Where(packet => packet.PacketType == typeof(SM_ATTACK) && packet.Get<int>("attackerObjId") == session.CharacterId)
					.GroupBy(packet => packet.Get<int>("targetObjId")).Select(group => group.Count()).OrderDescending().ToArray();
				Console.WriteLine($"{id}: class {player.GetPlayerClass()} ({(int)player.GetPlayerClass().GetClassId()}), level {player.GetLevel()}, " +
					$"HP {player.GetLifeStats().GetCurrentHp()}/{player.GetLifeStats().GetMaxHp()}, " +
					$"class page {contract.ClassChoice.ClassPageId}, action {contract.ClassChoice.Action}, masteries {string.Join(' ', contract.ClassChoice.MasterySkillIds)}, " +
					$"Q{contract.CeremonyReward.QuestId} var {ceremony.Var} at preceptor {ceremony.NpcId}, " +
					$"starter masteries still held {string.Join(' ', starterMasteries.Where(mastery => player.GetSkillList().IsSkillPresent(mastery)))}, " +
					$"ceremony item {contract.CeremonyReward.ItemId} by {contract.CeremonyReward.Action} from {contract.CeremonyReward.SelectableList}, " +
					$"Q{contract.Dispatch.QuestId} {dispatch.GetStatus()}/{dispatch.GetQuestVarById(0)}. " +
					$"Swings carried out in the trial, by opponent: {string.Join(' ', swings)} (Hellion's bound {row.TrialSwings ?? 600}).");
			});
		await CapitalAscensionScenario.RunAsmodianAsync(driver, contract, token, stopAtDispatchStart: true, trialSwings: row.TrialSwings);
		policy.AssertClean();
	}
}
