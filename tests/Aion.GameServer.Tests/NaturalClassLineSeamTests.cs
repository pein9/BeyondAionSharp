using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.GameServer.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

/// <summary>
/// CP-25: the identity rules by class line and the Ascension bridge by line and by choice. The accepted line classifies
/// as before and its bridge is the reviewed file's, value for value; every other pair's bridge is recomputed from
/// quest_data.xml and the ported quest handlers.
/// </summary>
public sealed class NaturalClassLineSeamTests
{
	private const int Ishalgen = 220010000, Q2002Instance = 320010000, Ataxiar = 320020000, Pandaemonium = 120010000,
		Altgard = 220030000, Convent = 120020000, SpaceOfDestiny = 320070000, Haramel = 300200000, Poeta = 210010000;

	// Lines of this test only: no account and no name is given to a class here.
	private static readonly NaturalClassLine PriestChanter = new("test-priest-chanter", PlayerClass.PRIEST, PlayerClass.CHANTER, 0, "Unused"),
		WarriorTemplar = new("test-warrior-templar", PlayerClass.WARRIOR, PlayerClass.TEMPLAR, 0, "Unused"),
		WarriorOnly = new("test-warrior", PlayerClass.WARRIOR, null, 0, "Unused");

	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

	[Fact]
	public void TheAcceptedLineClassifiesAsTheRuleDidBeforeTheSeam()
	{
		// The rule as it stood before CP-25, restated: the overloads without a line and the accepted line must both give it.
		static NaturalJourneyStage? Before(PlayerClass playerClass, int level, int? worldId, string? altgardLeg)
		{
			if (playerClass == PlayerClass.PRIEST && level is >= 1 and <= 9 &&
				(worldId is null || NaturalJourneyIdentityRules.PriestMaps.Contains(worldId.Value)))
				return NaturalJourneyStage.IshalgenPriest;
			if (playerClass == PlayerClass.CLERIC && level >= 9 && (worldId is null || NaturalJourneyIdentityRules.ClericMaps.Contains(worldId.Value) ||
				level >= 10 && worldId == 120020000 ||
				altgardLeg == "l11" && level >= 20 && worldId == 320070000 ||
				altgardLeg == "l12" && level >= 16 && worldId == 300200000 ||
				altgardLeg == NaturalAbyssEntry.Leg && level >= 25 && worldId is NaturalAbyssEntry.Morheim or NaturalAbyssEntry.ArenaMap))
				return NaturalJourneyStage.AscensionCleric;
			return null;
		}

		int rows = 0, accepted = 0;
		foreach (PlayerClass playerClass in Enum.GetValues<PlayerClass>())
		foreach (int level in new[] { 0, 1, 5, 9, 10, 15, 16, 19, 20, 24, 25, 26 })
		foreach (int? map in new int?[] { null, Ishalgen, Q2002Instance, Ataxiar, Pandaemonium, Altgard, Convent, SpaceOfDestiny, Haramel,
			NaturalAbyssEntry.Morheim, NaturalAbyssEntry.ArenaMap, Poeta })
		foreach (string? leg in new[] { null, "l11", "l12", NaturalAbyssEntry.Leg, "other" })
		{
			NaturalJourneyStage? expected = Before(playerClass, level, map, leg);
			Assert.Equal(expected, Try(() => NaturalJourneyIdentityRules.Classify(playerClass, level, map, leg)));
			Assert.Equal(expected, Try(() => NaturalJourneyIdentityRules.Classify((int)playerClass.GetClassId(), level, map, leg)));
			Assert.Equal(expected, Try(() => NaturalJourneyIdentityRules.Classify(NaturalClassLine.PriestCleric, playerClass, level, map, leg)));
			Assert.Equal(expected, Try(() => NaturalJourneyIdentityRules.Classify(NaturalClassLine.PriestCleric, (int)playerClass.GetClassId(), level, map, leg)));
			rows++;
			if (expected != null) accepted++;
		}
		Assert.Equal(Enum.GetValues<PlayerClass>().Length * 12 * 12 * 5, rows);
		Assert.True(accepted > 100, $"Only {accepted} accepted rows.");
		Assert.Throws<InvalidDataException>(() => NaturalJourneyIdentityRules.Classify(NaturalClassLine.PriestCleric, 250, 5, null));
	}

	[Theory]
	// The starter before Ascension: level 1-9 on Ishalgen, Q2002's instance and Ataxiar.
	[InlineData("chanter", PlayerClass.PRIEST, 1, Ishalgen, null, NaturalJourneyStage.IshalgenPriest)]
	[InlineData("chanter", PlayerClass.PRIEST, 9, Q2002Instance, null, NaturalJourneyStage.IshalgenPriest)]
	[InlineData("chanter", PlayerClass.PRIEST, 9, Ataxiar, null, NaturalJourneyStage.IshalgenPriest)]
	[InlineData("templar", PlayerClass.WARRIOR, 1, Ishalgen, null, NaturalJourneyStage.IshalgenPriest)]
	[InlineData("templar", PlayerClass.WARRIOR, 9, Ataxiar, null, NaturalJourneyStage.IshalgenPriest)]
	[InlineData("warrior", PlayerClass.WARRIOR, 9, Ishalgen, null, NaturalJourneyStage.IshalgenPriest)]
	[InlineData("warrior", PlayerClass.WARRIOR, 9, Ataxiar, null, NaturalJourneyStage.IshalgenPriest)]
	// The second class on the bridge maps.
	[InlineData("chanter", PlayerClass.CHANTER, 9, Ataxiar, null, NaturalJourneyStage.AscensionCleric)]
	[InlineData("chanter", PlayerClass.CHANTER, 9, Ishalgen, null, NaturalJourneyStage.AscensionCleric)]
	[InlineData("chanter", PlayerClass.CHANTER, 10, Pandaemonium, null, NaturalJourneyStage.AscensionCleric)]
	[InlineData("chanter", PlayerClass.CHANTER, 10, Altgard, null, NaturalJourneyStage.AscensionCleric)]
	[InlineData("templar", PlayerClass.TEMPLAR, 9, Ataxiar, null, NaturalJourneyStage.AscensionCleric)]
	[InlineData("templar", PlayerClass.TEMPLAR, 10, Altgard, null, NaturalJourneyStage.AscensionCleric)]
	// Refused: a starter past level 9 or off its maps, another line's class, the line's other second class.
	[InlineData("chanter", PlayerClass.PRIEST, 10, Ishalgen, null, null)]
	[InlineData("chanter", PlayerClass.PRIEST, 9, Pandaemonium, null, null)]
	[InlineData("chanter", PlayerClass.CLERIC, 10, Altgard, null, null)]
	[InlineData("chanter", PlayerClass.CHANTER, 8, Ataxiar, null, null)]
	[InlineData("chanter", PlayerClass.CHANTER, 10, Poeta, null, null)]
	[InlineData("templar", PlayerClass.PRIEST, 5, Ishalgen, null, null)]
	[InlineData("templar", PlayerClass.GLADIATOR, 10, Altgard, null, null)]
	[InlineData("templar", PlayerClass.CLERIC, 10, Altgard, null, null)]
	[InlineData("templar", PlayerClass.WARRIOR, 10, Ishalgen, null, null)]
	// The Convent and the leg-scoped maps are the Cleric's and stay tied to the Cleric.
	[InlineData("chanter", PlayerClass.CHANTER, 10, Convent, null, null)]
	[InlineData("chanter", PlayerClass.CHANTER, 24, SpaceOfDestiny, "l11", null)]
	[InlineData("chanter", PlayerClass.CHANTER, 24, Haramel, "l12", null)]
	[InlineData("chanter", PlayerClass.CHANTER, 26, NaturalAbyssEntry.Morheim, "ax", null)]
	[InlineData("chanter", PlayerClass.CHANTER, 26, NaturalAbyssEntry.ArenaMap, "ax", null)]
	[InlineData("templar", PlayerClass.TEMPLAR, 10, Convent, null, null)]
	[InlineData("templar", PlayerClass.TEMPLAR, 26, NaturalAbyssEntry.Morheim, "ax", null)]
	// A line with no second class has no state after Ascension.
	[InlineData("warrior", PlayerClass.GLADIATOR, 9, Ataxiar, null, null)]
	[InlineData("warrior", PlayerClass.TEMPLAR, 9, Ataxiar, null, null)]
	[InlineData("warrior", PlayerClass.TEMPLAR, 10, Altgard, null, null)]
	[InlineData("warrior", PlayerClass.GLADIATOR, 10, Pandaemonium, null, null)]
	[InlineData("warrior", PlayerClass.WARRIOR, 10, Ishalgen, null, null)]
	[InlineData("warrior", PlayerClass.CLERIC, 10, Altgard, null, null)]
	public void EveryLineHasItsStarterBeforeAscensionAndItsSecondClassAfter(string lineName, PlayerClass playerClass, int level, int map,
		string? leg, NaturalJourneyStage? expected)
	{
		Assert.Equal("ax", NaturalAbyssEntry.Leg);
		NaturalClassLine line = lineName switch { "chanter" => PriestChanter, "templar" => WarriorTemplar, _ => WarriorOnly };
		Assert.Equal(expected, Try(() => NaturalJourneyIdentityRules.Classify(line, playerClass, level, map, leg)));
		Assert.Equal(expected, Try(() => NaturalJourneyIdentityRules.Classify(line, (int)playerClass.GetClassId(), level, map, leg)));
		// A map that is not shown is not a reason to refuse; the class and the level still are.
		NaturalJourneyStage? unshown = Try(() => NaturalJourneyIdentityRules.Classify(line, playerClass, level, null, leg));
		if (expected != null) Assert.Equal(expected, unshown);
		if (unshown == null)
			Assert.Contains($"{playerClass} level {level}", Assert.Throws<InvalidDataException>(
				() => NaturalJourneyIdentityRules.Classify(line, playerClass, level, map, leg)).Message, StringComparison.Ordinal);
	}

	[Fact]
	public void TheAcceptedLinesBridgeIsTheReviewedFileRecordForRecord()
	{
		NaturalAscensionContract file = NaturalAscensionContract.LoadDefault();
		string expected = JsonSerializer.Serialize(file, Json);
		NaturalAscensionContract line = NaturalAscensionContract.ForLine(NaturalClassLine.PriestCleric);
		NaturalAscensionContract choice = NaturalAscensionContract.ForChoice(file, Lines.Value, PlayerClass.PRIEST, PlayerClass.CLERIC);
		NaturalAscensionContract picked = NaturalAscensionContract.ForChoice(file, Lines.Value, PlayerClass.PRIEST, PlayerClass.CLERIC,
			file.CeremonyReward.ItemId);
		foreach (NaturalAscensionContract built in new[] { line, choice, picked })
		{
			// Built in memory from the class-line contract, not handed back: the class-dependent parts are new objects.
			Assert.NotSame(file.Steps, built.Steps);
			Assert.NotSame(file.ClassChoice, built.ClassChoice);
			Assert.NotSame(file.Dispatch.ClassesPermitted, built.Dispatch.ClassesPermitted);
			Assert.Equal(expected, JsonSerializer.Serialize(built, Json));
		}
		// The serialized form holds every value of the file: reading it back gives the file's records again.
		using JsonDocument source = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "parity-artifacts/e2e/natural-ascension-contract.json")));
		using JsonDocument written = JsonDocument.Parse(expected);
		foreach (JsonProperty property in source.RootElement.EnumerateObject())
			Assert.True(written.RootElement.TryGetProperty(property.Name, out _), $"The contract record does not hold {property.Name}.");

		// The four class-dependent steps by role are the file's four keys.
		Assert.Equal("q2008-v6-munin-class", file.Step(NaturalAscensionStepRole.ClassChoice).Key);
		Assert.Equal("q2009-reward-lyfjaberga", file.Step(NaturalAscensionStepRole.Ceremony).Key);
		Assert.Equal("q2904-v0-doman", file.Step(NaturalAscensionStepRole.DispatchStart).Key);
		Assert.Equal("q2904-reward-meiyer", file.Step(NaturalAscensionStepRole.DispatchReward).Key);
		Assert.Equal(4, Enum.GetValues<NaturalAscensionStepRole>().Select(file.Step).Distinct().Count());
	}

	public static TheoryData<PlayerClass, int> Picks()
	{
		var rows = new TheoryData<PlayerClass, int>();
		foreach (NaturalSecondClass second in NaturalClassLineContract.LoadDefault().SecondClasses)
			for (int index = 0; index < second.CeremonyReward.Items.Length; index++)
				rows.Add(second.PlayerClass, index);
		return rows;
	}

	[Theory]
	[MemberData(nameof(Picks))]
	public void EveryPairsBridgeIsRecomputedFromQuestDataAndTheHandlers(PlayerClass second, int pickIndex)
	{
		NaturalAscensionContract core = NaturalAscensionContract.LoadDefault();
		PlayerClass starter = second.GetStartingClass();
		string list = SelectableList(second);
		int[] offered = Quest(2009).Elements(list).Select(node => (int)node.Attribute("item_id")!).ToArray();
		int pick = offered[pickIndex];
		NaturalAscensionContract bridge = NaturalAscensionContract.ForChoice(core, Lines.Value, starter, second, pick);

		// Q2008: the action that sets this class at var 6, and the starter's class page.
		Match choice = Regex.Match(Handler("_2008Ascension.cs"),
			$@"case DialogAction\.(SETPRO\d+):\s*return var == 6 && SetPlayerClass\(env, qs, PlayerClass\.{second}\);");
		Assert.True(choice.Success, $"No Q2008 class-choice branch for {second}.");
		string action = choice.Groups[1].Value;
		int classPage = ClassChangeService.GetClassSelectionDialogPageId(Race.ASMODIANS, starter);
		Assert.Equal((2008, starter.ToString(), second.ToString(), classPage, action),
			(bridge.ClassChoice.QuestId, bridge.ClassChoice.FromClass, bridge.ClassChoice.ToClass, bridge.ClassChoice.ClassPageId, bridge.ClassChoice.Action));
		Assert.Equal(Lines.Value.Second(second).Masteries.Select(mastery => mastery.SkillId), bridge.ClassChoice.MasterySkillIds);
		Assert.Equal(starter.ToString(), bridge.Start.Class);
		NaturalAscensionStep choiceStep = bridge.Step(NaturalAscensionStepRole.ClassChoice);
		Assert.Equal("q2008-v6-munin-class", choiceStep.Key);
		Assert.Equal(6, choiceStep.Var);
		Assert.Equal(new[] { "QUEST_SELECT", "SETPRO6", action }, choiceStep.Actions);
		Assert.Equal(new[] { 2716, classPage }, choiceStep.Pages);

		// Q2009: Balder sets the var and the reward group by starting class; only that class's preceptor pays, from the
		// second class's own list.
		string ceremony = Handler("_2009ACeremonyinPandaemonium.cs");
		Match byStarter = Regex.Match(ceremony, $@"case PlayerClass\.{starter}:\s*qs\.SetQuestVar\((\d+)\);\s*qs\.SetRewardGroup\((\d+)\);");
		Assert.True(byStarter.Success, $"No Q2009 var and reward group for {starter}.");
		int ceremonyVar = Int(byStarter, 1), rewardGroup = Int(byStarter, 2);
		Match preceptor = Regex.Match(ceremony, $@"targetId == (\d+) && var == {ceremonyVar}\)\s*\{{\s*switch \(env\.GetDialogActionId\(\)\)\s*\{{\s*" +
			@"case DialogAction\.USE_OBJECT:\s*return SendQuestDialog\(env, (\d+)\);");
		Assert.True(preceptor.Success, $"No Q2009 preceptor branch for {starter}.");
		NaturalAscensionStep ceremonyStep = bridge.Step(NaturalAscensionStepRole.Ceremony);
		Assert.Equal((2009, "REWARD", (int?)ceremonyVar, Int(preceptor, 1)),
			(ceremonyStep.QuestId, ceremonyStep.ExpectedStatus, ceremonyStep.Var, ceremonyStep.NpcId));
		Assert.Equal(new[] { Int(preceptor, 2), 8 }, ceremonyStep.Pages);
		Assert.Equal(new[] { "USE_OBJECT", "SELECT_QUEST_REWARD", $"SELECTED_QUEST_REWARD{pickIndex + 1}" }, ceremonyStep.Actions);
		Assert.Equal(DialogAction.SELECTED_QUEST_REWARD1 + pickIndex, NaturalAscensionContract.DialogActionId(bridge.CeremonyReward.Action));
		Assert.Equal(Lines.Value.Starter(starter).Ceremony.PreceptorPosition, ceremonyStep.Position);
		Assert.Equal(core.Step(NaturalAscensionStepRole.Ceremony).MapId, ceremonyStep.MapId);
		Assert.Equal(starter == PlayerClass.PRIEST ? "q2009-reward-lyfjaberga" : $"q2009-reward-preceptor-{ceremonyStep.NpcId}", ceremonyStep.Key);
		Assert.Equal((2009, rewardGroup, list, ceremonyStep.Actions[^1], pick),
			(bridge.CeremonyReward.QuestId, bridge.CeremonyReward.RewardGroup, bridge.CeremonyReward.SelectableList, bridge.CeremonyReward.Action,
				bridge.CeremonyReward.ItemId));
		Assert.Equal(Lines.Value.Second(second).CeremonyReward.Items[pickIndex].ItemGroup, bridge.CeremonyReward.ItemGroup);
		// Every reward group of Q2009 pays the same Kinah and tea, so those two stay the reviewed bridge's.
		XElement group = Quest(2009).Elements("rewards").ElementAt(rewardGroup);
		Assert.Equal(bridge.CeremonyReward.Kinah, (long)group.Elements("reward_item").Single(item => (int)item.Attribute("item_id")! == 182400001).Attribute("count")!);
		Assert.Equal(bridge.CeremonyReward.TeaCount, (int)group.Elements("reward_item").Single(item => (int)item.Attribute("item_id")! == bridge.CeremonyReward.TeaItemId).Attribute("count")!);

		// The dispatch quest: the one Q2009's reward group starts for this class. Its handler is the same two NPCs.
		XElement dispatch = Assert.Single(QuestData.Value.Elements("quest"), quest =>
			(string?)quest.Attribute("race_permitted") == "ASMODIANS" &&
			quest.Element("start_conditions")?.Elements("finished").Any(node => (int?)node.Attribute("quest_id") == 2009 &&
				node.Attribute("reward") != null) == true &&
			((string?)quest.Element("class_permitted") ?? "").Split(' ').Contains(second.ToString()));
		int dispatchId = (int)dispatch.Attribute("id")!;
		Assert.Equal((dispatchId, 2009, rewardGroup, (int)dispatch.Descendants("quest_work_item").Single().Attribute("item_id")!),
			(bridge.Dispatch.QuestId, bridge.Dispatch.StartsAfter, bridge.Dispatch.StartReward, bridge.Dispatch.UnhandedWorkItemId));
		Assert.Equal(rewardGroup, (int)Assert.Single(dispatch.Element("start_conditions")!.Elements("finished")).Attribute("reward")!);
		Assert.Equal(((string)dispatch.Element("class_permitted")!).Split(' '), bridge.Dispatch.ClassesPermitted);
		NaturalAscensionQuest dispatchQuest = Assert.Single(bridge.Quests, quest => quest.Id == dispatchId);
		Assert.Equal(((string)dispatch.Attribute("category")!, (int?)dispatch.Attribute("minlevel_permitted"), (int?)dispatch.Element("rewards")!.Attribute("exp"), (int?)2009),
			(dispatchQuest.Category, (int?)dispatchQuest.MinimumLevel, (int?)dispatchQuest.RewardExperience, dispatchQuest.Prerequisite));
		Assert.Equal(core.Quests.Length, bridge.Quests.Length);
		NaturalAscensionStep start = bridge.Step(NaturalAscensionStepRole.DispatchStart), reward = bridge.Step(NaturalAscensionStepRole.DispatchReward);
		Assert.Equal(($"q{dispatchId}-v0-doman", $"q{dispatchId}-reward-meiyer"), (start.Key, reward.Key));
		string handler = File.ReadAllText(Assert.Single(Directory.GetFiles(
			Path.Combine(Root(), "src/Aion.GameServer/Handlers/Quest/ascension"), $"_{dispatchId}*DispatchtoAltgard.cs")));
		Assert.Contains($"base({dispatchId})", handler, StringComparison.Ordinal);
		foreach (NaturalAscensionStep step in new[] { start, reward })
			Assert.Contains($"qe.RegisterQuestNpc({step.NpcId}).AddOnTalkEvent(questId);", handler, StringComparison.Ordinal);
		Assert.Equal(2, Regex.Matches(handler, @"qe\.RegisterQuestNpc\(").Count);
		// But for its quest id, the handler is the Cleric's and Chanter's Q2904 handler, so the two reviewed steps hold.
		static string Neutral(string source, int questId) => Regex.Replace(source.Replace(questId.ToString(CultureInfo.InvariantCulture), "N",
			StringComparison.Ordinal), @"@author[^\r\n]*|_NA(?=Dispatch)", match => match.Value.StartsWith('@') ? "" : "_N");
		Assert.Equal(Neutral(Handler("_2904DispatchtoAltgard.cs"), 2904), Neutral(handler, dispatchId));
		NaturalAscensionStep[] coreDispatch = [core.Step(NaturalAscensionStepRole.DispatchStart), core.Step(NaturalAscensionStepRole.DispatchReward)];
		Assert.Equal(JsonSerializer.Serialize(coreDispatch.Select(step => step with { Key = "", QuestId = 0 }), Json),
			JsonSerializer.Serialize(new[] { start, reward }.Select(step => step with { Key = "", QuestId = 0 }), Json));

		// The endpoint and what the bridge protects.
		Assert.Equal((second.ToString(), (int)second.GetClassId()), (bridge.Endpoint.Class, bridge.Endpoint.ClassId));
		Assert.Equal(new[] { 2008, 2009, dispatchId, 24010 }, bridge.Endpoint.CompletedQuestIds);
		Assert.Equal(new[] { pick }, bridge.Endpoint.EquippedItemIds);
		Assert.Contains(pick, bridge.ProtectedItemIds);
		Assert.Equal(core.ProtectedItemIds.Length, bridge.ProtectedItemIds.Length);
		if (pick != core.CeremonyReward.ItemId) Assert.DoesNotContain(core.CeremonyReward.ItemId, bridge.ProtectedItemIds);

		// Nothing else moved: the steps of Q2008, Q2009 and Q24010 that no class pair changes, and the parts no pair touches.
		Assert.Equal(core.Steps.Length, bridge.Steps.Length);
		Assert.Equal(bridge.Steps.Length, bridge.Steps.Select(step => step.Key).Distinct().Count());
		for (int index = 0; index < core.Steps.Length; index++)
			if (!new[] { choiceStep, ceremonyStep, start, reward }.Contains(bridge.Steps[index]))
				Assert.Same(core.Steps[index], bridge.Steps[index]);
		Assert.Equal(11, bridge.Steps.Count(step => core.Steps.Any(original => ReferenceEquals(original, step))));
		Assert.Same(core.Instance, bridge.Instance);
		Assert.Same(core.Teleporter, bridge.Teleporter);
		Assert.Same(core.Bind, bridge.Bind);
		Assert.Same(core.Shop, bridge.Shop);
		Assert.Same(core.Movies, bridge.Movies);
		Assert.Same(core.KeptAccessories, bridge.KeptAccessories);
		Assert.Equal((core.SchemaVersion, core.JavaReference), (bridge.SchemaVersion, bridge.JavaReference));
		Assert.Equal(core.Start with { Class = starter.ToString() }, bridge.Start);
	}

	[Fact]
	public void TheChanterAndTheTemplarBridges()
	{
		NaturalAscensionContract core = NaturalAscensionContract.LoadDefault();

		// The Chanter: the Priest's other choice. With no pick named it keeps the reviewed pick, the Karmic Staff, which its
		// list offers (CP-Q7's default).
		NaturalAscensionContract chanter = NaturalAscensionContract.ForLine(PriestChanter);
		Assert.Equal(("SETPRO13", 11, "chanter_selectable_reward", 40, 2904),
			(chanter.ClassChoice.Action, chanter.Endpoint.ClassId, chanter.CeremonyReward.SelectableList,
				chanter.Step(NaturalAscensionStepRole.Ceremony).Var!.Value, chanter.Dispatch.QuestId));
		Assert.Equal((101500498, "STAFF", "SELECTED_QUEST_REWARD2"),
			(chanter.CeremonyReward.ItemId, chanter.CeremonyReward.ItemGroup, chanter.CeremonyReward.Action));
		Assert.Equal(core.Steps.Select(step => step.Key), chanter.Steps.Select(step => step.Key));
		// All that differs from the Cleric's bridge is the class, the action and the list.
		Assert.Equal(JsonSerializer.Serialize(core, Json), JsonSerializer.Serialize(chanter, Json)
			.Replace("SETPRO13", "SETPRO14", StringComparison.Ordinal)
			.Replace("chanter_selectable_reward", "priest_selectable_reward", StringComparison.Ordinal)
			.Replace("\"toClass\":\"CHANTER\"", "\"toClass\":\"CLERIC\"", StringComparison.Ordinal)
			.Replace("\"class\":\"CHANTER\",\"classId\":11", "\"class\":\"CLERIC\",\"classId\":10", StringComparison.Ordinal));
		NaturalAscensionContract mace = NaturalAscensionContract.ForLine(PriestChanter, 100100495);
		Assert.Equal((100100495, "MACE", "SELECTED_QUEST_REWARD1"), (mace.CeremonyReward.ItemId, mace.CeremonyReward.ItemGroup, mace.CeremonyReward.Action));

		// The Templar: another starter, so another class page, var, preceptor, list and dispatch quest.
		NaturalAscensionContract templar = NaturalAscensionContract.ForLine(WarriorTemplar, 100900488);
		Assert.Equal(("SETPRO8", 2, "knight_selectable_reward", 10, 2901),
			(templar.ClassChoice.Action, templar.Endpoint.ClassId, templar.CeremonyReward.SelectableList,
				templar.Step(NaturalAscensionStepRole.Ceremony).Var!.Value, templar.Dispatch.QuestId));
		Assert.Equal((3057, 0, 204080, 2034), (templar.ClassChoice.ClassPageId, templar.CeremonyReward.RewardGroup,
			templar.Step(NaturalAscensionStepRole.Ceremony).NpcId, templar.Step(NaturalAscensionStepRole.Ceremony).Pages[0]));
		Assert.Equal((100900488, "GREATSWORD", "SELECTED_QUEST_REWARD2"),
			(templar.CeremonyReward.ItemId, templar.CeremonyReward.ItemGroup, templar.CeremonyReward.Action));
		Assert.Equal(new[] { "GLADIATOR", "TEMPLAR" }, templar.Dispatch.ClassesPermitted);
		Assert.Equal(new[] { "q2009-reward-preceptor-204080", "q2901-v0-doman", "q2901-reward-meiyer" },
			templar.Steps.Select(step => step.Key).Except(core.Steps.Select(step => step.Key)));

		// Refused, by name: a pick the list does not offer (a Templar is not handed the Cleric's staff), a pair that is not
		// a starter and its second class, and a line that takes no second class.
		Assert.Contains("knight_selectable_reward does not offer item 101500498",
			Assert.Throws<InvalidDataException>(() => NaturalAscensionContract.ForLine(WarriorTemplar)).Message, StringComparison.Ordinal);
		Assert.Throws<InvalidDataException>(() => NaturalAscensionContract.ForLine(PriestChanter, 100000640));
		Assert.Contains("CLERIC is not a second class of WARRIOR", Assert.Throws<InvalidDataException>(
			() => NaturalAscensionContract.ForChoice(core, Lines.Value, PlayerClass.WARRIOR, PlayerClass.CLERIC)).Message, StringComparison.Ordinal);
		Assert.Throws<InvalidDataException>(() => NaturalAscensionContract.ForChoice(core, Lines.Value, PlayerClass.CLERIC, PlayerClass.CLERIC));
		Assert.Contains("test-warrior takes no second class",
			Assert.Throws<InvalidOperationException>(() => NaturalAscensionContract.ForLine(WarriorOnly)).Message, StringComparison.Ordinal);
		// The reviewed file is not written to by any of this.
		Assert.Equal(JsonSerializer.Serialize(NaturalAscensionContract.LoadDefault(), Json), JsonSerializer.Serialize(core, Json));
	}

	/// <summary>The selectable-reward element QuestTemplate reads for a class, as quest_data.xml names it.</summary>
	private static string SelectableList(PlayerClass playerClass)
	{
		string template = File.ReadAllText(Path.Combine(Root(), "src/Aion.GameServer/Model/Templates/QuestTemplate.cs"));
		Match field = Regex.Match(template, $@"case PlayerClass\.{playerClass}:\s*return (\w+) == null");
		Assert.True(field.Success, $"QuestTemplate has no selectable reward for {playerClass}.");
		Match element = Regex.Match(template, $@"\[XmlElement\(""(\w+)""\)\]\s*public List<QuestItems> {field.Groups[1].Value};");
		Assert.True(element.Success);
		return element.Groups[1].Value;
	}

	private static NaturalJourneyStage? Try(Func<NaturalJourneyStage> classify)
	{
		try
		{
			return classify();
		}
		catch (InvalidDataException)
		{
			return null;
		}
	}

	private static readonly Lazy<NaturalClassLineContract> Lines = new(NaturalClassLineContract.LoadDefault);
	private static readonly Lazy<XElement> QuestData = new(() =>
		XDocument.Load(Path.Combine(Root(), "game-server/data/static_data/quest_data/quest_data.xml")).Root!);

	private static XElement Quest(int id) => Assert.Single(QuestData.Value.Elements("quest"), quest => (int?)quest.Attribute("id") == id);
	private static int Int(Match match, int group) => int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);
	private static string Handler(string file) => File.ReadAllText(Path.Combine(Root(), "src/Aion.GameServer/Handlers/Quest/ascension", file));
	private static string Root() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
}
