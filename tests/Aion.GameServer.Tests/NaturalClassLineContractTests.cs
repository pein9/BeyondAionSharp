using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.GameServer.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

/// <summary>
/// CP-01: the class-line contract for the six Asmodian starters and the eleven second classes, recomputed from the
/// shipped static data, the ported quest handlers and the checked-in page map of the 4.8 client's quest dialogs.
/// </summary>
public sealed class NaturalClassLineContractTests
{
	private const string IshalgenSpawns = "Npcs/220010000_Ishalgen.xml";
	private const string PandaemoniumSpawns = "Npcs/120010000_Pandaemonium.xml";

	public static TheoryData<string> Classes => new(Enum.GetNames<PlayerClass>());

	[Fact]
	public void ContractHoldsEveryStarterAndEverySecondClassOnce()
	{
		NaturalClassLineContract contract = Contract.Value;
		PlayerClass[] all = Enum.GetValues<PlayerClass>();
		Assert.Equal(all.Where(pc => pc.IsStartingClass()).Select(pc => pc.ToString()).Order(), contract.Starters.Select(row => row.Class).Order());
		Assert.Equal(all.Where(pc => !pc.IsStartingClass()).Select(pc => pc.ToString()).Order(), contract.SecondClasses.Select(row => row.Class).Order());
		Assert.Equal(6, contract.Starters.Length);
		Assert.Equal(11, contract.SecondClasses.Length);
		Assert.Equal("ASMODIANS", contract.Race);
		foreach (int questId in new[] { contract.NewSkillQuestId, contract.AscensionQuestId, contract.CeremonyQuestId })
			Assert.Equal(contract.Race, (string?)Quest(questId).Attribute("race_permitted"));
	}

	[Theory]
	[MemberData(nameof(Classes))]
	public void EveryValueOfAClassRowIsRecomputedFromShippedDataAndTheHandlers(string className)
	{
		PlayerClass playerClass = Enum.Parse<PlayerClass>(className);
		if (playerClass.IsStartingClass())
			AssertStarter(Contract.Value.Starter(playerClass));
		else
			AssertSecondClass(Contract.Value.Second(playerClass));
	}

	[Fact]
	public void PriestAndClericRowsEqualTheAscensionContract()
	{
		NaturalClassLineContract contract = Contract.Value;
		NaturalAscensionContract ascension = NaturalAscensionContract.LoadDefault();
		NaturalStarterClass priest = contract.Starter(PlayerClass.PRIEST);
		NaturalSecondClass cleric = contract.Second(PlayerClass.CLERIC);
		Assert.Equal(ascension.JavaReference, contract.JavaReference);
		Assert.Equal(ascension.Start.Race, contract.Race);
		Assert.Equal(ascension.Start.Class, priest.Class);

		// natural-ascension-contract.json, the step q2009-reward-lyfjaberga.
		NaturalAscensionStep reward = Assert.Single(ascension.Steps, step => step.Key == "q2009-reward-lyfjaberga");
		Assert.Equal(contract.CeremonyQuestId, reward.QuestId);
		Assert.Equal(reward.Var, priest.Ceremony.Var);
		Assert.Equal(reward.NpcId, priest.Ceremony.PreceptorNpcId);
		Assert.Equal(reward.Position, priest.Ceremony.PreceptorPosition);
		Assert.Equal(reward.TalkRange, priest.Ceremony.TalkRange);
		Assert.Equal(reward.Pages[0], priest.Ceremony.PageId);

		// classChoice.
		Assert.Equal(ascension.ClassChoice.QuestId, contract.AscensionQuestId);
		Assert.Equal(ascension.ClassChoice.FromClass, cleric.Parent);
		Assert.Equal(ascension.ClassChoice.ToClass, cleric.Class);
		Assert.Equal(ascension.ClassChoice.ClassPageId, priest.ClassPage.PageId);
		Assert.Equal(ascension.ClassChoice.Action, cleric.Action);
		Assert.Equal(ascension.ClassChoice.MasterySkillIds, cleric.Masteries.Select(mastery => mastery.SkillId));
		NaturalAscensionStep choice = Assert.Single(ascension.Steps, step => step.Actions.Contains(cleric.Action));
		Assert.Equal(contract.ClassChoiceVar, choice.Var);
		Assert.Contains(priest.ClassPage.PageId, choice.Pages);

		// ceremonyReward.
		Assert.Equal(ascension.CeremonyReward.QuestId, contract.CeremonyQuestId);
		Assert.Equal(ascension.CeremonyReward.RewardGroup, priest.Ceremony.RewardGroup);
		Assert.Equal(ascension.CeremonyReward.SelectableList, cleric.CeremonyReward.SelectableList);
		NaturalSecondClassRewardItem picked = cleric.CeremonyReward.Items[
			NaturalAscensionContract.DialogActionId(ascension.CeremonyReward.Action) - DialogAction.SELECTED_QUEST_REWARD1];
		Assert.Equal(ascension.CeremonyReward.ItemId, picked.ItemId);
		Assert.Equal(ascension.CeremonyReward.ItemGroup, picked.ItemGroup);

		// dispatch.
		Assert.Equal(ascension.Dispatch.QuestId, cleric.Dispatch.QuestId);
		Assert.Equal(ascension.Dispatch.StartsAfter, contract.CeremonyQuestId);
		Assert.Equal(ascension.Dispatch.StartReward, cleric.Dispatch.StartReward);
		Assert.Equal(ascension.Dispatch.UnhandedWorkItemId, cleric.Dispatch.WorkItemId);
		Assert.Equal(ascension.Dispatch.ClassesPermitted.Order(),
			contract.SecondClasses.Where(row => row.Dispatch.QuestId == cleric.Dispatch.QuestId).Select(row => row.Class).Order());
	}

	[Fact]
	public void ContractJavaReferenceMatchesThePortState()
	{
		using JsonDocument state = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "docs/upstream-port-state.json")));
		Assert.Equal(Contract.Value.JavaReference, state.RootElement.GetProperty("lastCompletedJavaCommit").GetString());
	}

	private static void AssertStarter(NaturalStarterClass row)
	{
		NaturalClassLineContract contract = Contract.Value;
		PlayerClass playerClass = row.PlayerClass;
		Assert.Equal((int)playerClass.GetClassId(), row.ClassId);

		// Q2132 "A New Skill": the level hook sets the var and the reward group, and only the class's own trainer answers.
		string newSkill = Handler("ishalgen/_2132ANewSkill.cs");
		Assert.Contains($"base({contract.NewSkillQuestId})", newSkill, StringComparison.Ordinal);
		(int questVar, int group) = VarAndRewardGroup(newSkill, playerClass);
		Assert.Equal(questVar, row.NewSkill.Var);
		Assert.Equal(group, row.NewSkill.RewardGroup);
		Match trainer = Regex.Match(newSkill, $@"case (\d+):\s*if \(playerClass == PlayerClass\.{playerClass}\)\s*\{{\s*" +
			@"if \(env\.GetDialogActionId\(\) == DialogAction\.USE_OBJECT\)\s*return SendQuestDialog\(env, (\d+)\);");
		Assert.True(trainer.Success, $"No Q2132 trainer branch for {playerClass}.");
		Assert.Equal(Int(trainer, 1), row.NewSkill.TrainerNpcId);
		Assert.Equal(Int(trainer, 2), row.NewSkill.PageId);
		Assert.Contains($"qe.RegisterQuestNpc({row.NewSkill.TrainerNpcId}).AddOnTalkEvent(questId);", newSkill, StringComparison.Ordinal);
		Assert.True(Quest(contract.NewSkillQuestId).Elements("rewards").Count() > row.NewSkill.RewardGroup);
		AssertSpawn(IshalgenSpawns, row.NewSkill.TrainerNpcId, row.NewSkill.TrainerPosition);
		Assert.Equal(TalkDistances.Value[row.NewSkill.TrainerNpcId] + 1, row.NewSkill.TalkRange);

		// Q2008: SETPRO6 sends the class page at the class-choice var; the 4.8 client's page shows the class's own buttons.
		string ascension = Handler("ascension/_2008Ascension.cs");
		Assert.Contains("ClassChangeService.GetClassSelectionDialogPageId(player.GetRace(), player.GetPlayerClass())", ascension, StringComparison.Ordinal);
		Assert.Contains($"if (var == {contract.ClassChoiceVar} && dialogPageId != 0)", ascension, StringComparison.Ordinal);
		Assert.Equal(ClassChangeService.GetClassSelectionDialogPageId(Race.ASMODIANS, playerClass), row.ClassPage.PageId);
		Assert.Equal(NaturalAscensionContract.DialogActionId(row.ClassPage.ClientPage.ToUpperInvariant()), row.ClassPage.PageId);
		Assert.Equal(ByActionNumber(ClientClassPageActions(row.ClassPage.ClientPage)), row.ClassPage.ClientActions);
		Assert.Equal(ByActionNumber(contract.SecondClassesOf(playerClass).Select(second => second.Action)), row.ClassPage.ClientActions);

		// Q2009: Balder sets the var and the reward group by starting class, and only the class's preceptor pays.
		string ceremony = Handler("ascension/_2009ACeremonyinPandaemonium.cs");
		Assert.Contains($"base({contract.CeremonyQuestId})", ceremony, StringComparison.Ordinal);
		(questVar, group) = VarAndRewardGroup(ceremony, playerClass);
		Assert.Equal(questVar, row.Ceremony.Var);
		Assert.Equal(group, row.Ceremony.RewardGroup);
		Match preceptor = Regex.Match(ceremony, $@"targetId == (\d+) && var == {row.Ceremony.Var}\)\s*\{{\s*switch \(env\.GetDialogActionId\(\)\)\s*\{{\s*" +
			@"case DialogAction\.USE_OBJECT:\s*return SendQuestDialog\(env, (\d+)\);");
		Assert.True(preceptor.Success, $"No Q2009 preceptor branch for {playerClass}.");
		Assert.Equal(Int(preceptor, 1), row.Ceremony.PreceptorNpcId);
		Assert.Equal(Int(preceptor, 2), row.Ceremony.PageId);
		Assert.Contains($"qe.RegisterQuestNpc({row.Ceremony.PreceptorNpcId}).AddOnTalkEvent(questId);", ceremony, StringComparison.Ordinal);
		XElement ceremonyQuest = Quest(contract.CeremonyQuestId);
		Assert.Equal("1", (string?)ceremonyQuest.Attribute("use_class_reward"));
		Assert.True(ceremonyQuest.Elements("rewards").Count() > row.Ceremony.RewardGroup);
		AssertSpawn(PandaemoniumSpawns, row.Ceremony.PreceptorNpcId, row.Ceremony.PreceptorPosition);
		Assert.Equal(TalkDistances.Value[row.Ceremony.PreceptorNpcId] + 1, row.Ceremony.TalkRange);

		AssertMasteries(playerClass, contract.MasteryLevels.Starter, row.Masteries);
		Assert.All(row.Masteries, mastery => Assert.Null(mastery.ReplacesSkillId));
	}

	private static void AssertSecondClass(NaturalSecondClass row)
	{
		NaturalClassLineContract contract = Contract.Value;
		PlayerClass playerClass = row.PlayerClass;
		PlayerClass parent = playerClass.GetStartingClass();
		NaturalStarterClass starter = contract.Starter(parent);
		Assert.Equal((int)playerClass.GetClassId(), row.ClassId);
		Assert.Equal(parent.ToString(), row.Parent);

		// ClassChangeService.SetClass accepts only the starter's id + 1 and + 2.
		string service = File.ReadAllText(Path.Combine(Root(), "src/Aion.GameServer/Services/ClassChangeService.cs"));
		Assert.Contains("nc.GetClassId() <= id || nc.GetClassId() > id + 2", service, StringComparison.Ordinal);
		Assert.InRange(row.ClassId, starter.ClassId + 1, starter.ClassId + 2);

		Match choice = Regex.Match(Handler("ascension/_2008Ascension.cs"),
			$@"case DialogAction\.(SETPRO\d+):\s*return var == {contract.ClassChoiceVar} && SetPlayerClass\(env, qs, PlayerClass\.{playerClass}\);");
		Assert.True(choice.Success, $"No Q2008 class-choice branch for {playerClass}.");
		Assert.Equal(choice.Groups[1].Value, row.Action);

		AssertMasteries(playerClass, contract.MasteryLevels.SecondClass, row.Masteries);
		Assert.All(row.Masteries.Where(mastery => mastery.ReplacesSkillId != null),
			mastery => Assert.Contains(starter.Masteries, basic => basic.SkillId == mastery.ReplacesSkillId && basic.Unlocks == mastery.Unlocks));

		// Q2009 pays by class (use_class_reward): QuestTemplate picks the list, quest_data holds its items.
		string template = File.ReadAllText(Path.Combine(Root(), "src/Aion.GameServer/Model/Templates/QuestTemplate.cs"));
		Match field = Regex.Match(template, $@"case PlayerClass\.{playerClass}:\s*return (\w+) == null");
		Assert.True(field.Success, $"QuestTemplate has no selectable reward for {playerClass}.");
		Match element = Regex.Match(template, $@"\[XmlElement\(""(\w+)""\)\]\s*public List<QuestItems> {field.Groups[1].Value};");
		Assert.True(element.Success);
		Assert.Equal(element.Groups[1].Value, row.CeremonyReward.SelectableList);
		int[] items = Quest(contract.CeremonyQuestId).Elements(row.CeremonyReward.SelectableList).Select(node => (int)node.Attribute("item_id")!).ToArray();
		Assert.NotEmpty(items);
		Assert.Equal(items, row.CeremonyReward.Items.Select(item => item.ItemId));
		Assert.All(row.CeremonyReward.Items, item => Assert.Equal(ItemGroups.Value[item.ItemId], item.ItemGroup));

		// The dispatch quest: the one that Q2009's reward group starts for this class.
		XElement dispatch = Assert.Single(QuestData.Value.Elements("quest"), quest =>
			(string?)quest.Attribute("race_permitted") == contract.Race &&
			quest.Element("start_conditions")?.Elements("finished").Any(node => (int?)node.Attribute("quest_id") == contract.CeremonyQuestId &&
				node.Attribute("reward") != null) == true &&
			((string?)quest.Element("class_permitted") ?? "").Split(' ').Contains(row.Class));
		Assert.Equal((int)dispatch.Attribute("id")!, row.Dispatch.QuestId);
		Assert.Equal((int)Assert.Single(dispatch.Element("start_conditions")!.Elements("finished")).Attribute("reward")!, row.Dispatch.StartReward);
		Assert.Equal(starter.Ceremony.RewardGroup, row.Dispatch.StartReward);
		Assert.Equal((int)dispatch.Descendants("quest_work_item").Single().Attribute("item_id")!, row.Dispatch.WorkItemId);
		Assert.True(ItemGroups.Value.ContainsKey(row.Dispatch.WorkItemId));
		string handler = File.ReadAllText(Assert.Single(Directory.GetFiles(
			Path.Combine(Root(), "src/Aion.GameServer/Handlers/Quest/ascension"), $"_{row.Dispatch.QuestId}*DispatchtoAltgard.cs")));
		Assert.Contains($"base({row.Dispatch.QuestId})", handler, StringComparison.Ordinal);
	}

	private static (int Var, int RewardGroup) VarAndRewardGroup(string handler, PlayerClass starter)
	{
		Match match = Regex.Match(handler, $@"case PlayerClass\.{starter}:\s*qs\.SetQuestVar\((\d+)\);\s*qs\.SetRewardGroup\((\d+)\);");
		Assert.True(match.Success, $"No var and reward group for {starter}.");
		return (Int(match, 1), Int(match, 2));
	}

	private static void AssertMasteries(PlayerClass playerClass, int level, NaturalClassMastery[] actual)
	{
		var expected = SkillTree.Value.Elements("skill")
			.Where(node => (string?)node.Attribute("classId") == playerClass.ToString() && (int)node.Attribute("minLevel")! == level &&
				Masteries.Value.ContainsKey((int)node.Attribute("skillId")!))
			.Select(node =>
			{
				Assert.Null(node.Attribute("race"));
				Assert.Equal("true", (string?)node.Attribute("autolearn"));
				int skillId = (int)node.Attribute("skillId")!;
				return new NaturalClassMastery(skillId, Masteries.Value[skillId].Kind, Masteries.Value[skillId].Unlocks, (int?)node.Attribute("skillLearn"));
			})
			.OrderBy(mastery => mastery.SkillId)
			.ToArray();
		Assert.NotEmpty(expected);
		Assert.Equal(expected, actual);
	}

	/// <summary>The SETPRO actions a class page of the client's Q2008 dialog leads to: each button opens a page that holds one.</summary>
	private static IEnumerable<string> ClientClassPageActions(string pageName)
	{
		NaturalClassLineContract contract = Contract.Value;
		using JsonDocument map = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "parity-artifacts/e2e", contract.ClientDialogs.Map)));
		Assert.Equal(contract.ClientDialogs.ClientVersion, map.RootElement.GetProperty("client").GetProperty("version").GetString());
		Assert.Equal("verified", contract.ClientDialogs.ClassPageButtons);
		JsonElement quest = Assert.Single(map.RootElement.GetProperty("quests").EnumerateArray().ToArray(),
			node => node.GetProperty("questId").GetInt32() == contract.AscensionQuestId);
		Dictionary<string, string[]> pages = quest.GetProperty("pages").EnumerateArray().ToDictionary(
			page => page.GetProperty("page").GetString()!,
			page => page.GetProperty("buttons").EnumerateArray()
				.SelectMany(button => button.GetProperty("actions").EnumerateArray().Select(action => action.GetString()!)).ToArray());
		string[] opened = pages[pageName];
		Assert.All(opened, action => Assert.StartsWith(pageName.ToUpperInvariant() + "_", action, StringComparison.Ordinal));
		return opened.SelectMany(action => pages[action.ToLowerInvariant()]).Where(action => action.StartsWith("SETPRO", StringComparison.Ordinal));
	}

	private static string[] ByActionNumber(IEnumerable<string> actions) =>
		actions.OrderBy(action => int.Parse(action["SETPRO".Length..], CultureInfo.InvariantCulture)).ToArray();

	private static void AssertSpawn(string spawnFile, int npcId, float[] position)
	{
		XElement spot = Assert.Single(XDocument.Load(Data("spawns", spawnFile)).Root!.Descendants("spawn")
			.Where(spawn => (int?)spawn.Attribute("npc_id") == npcId).Elements("spot"));
		Assert.Equal(3, position.Length);
		Assert.True(MathF.Abs((float)spot.Attribute("x")! - position[0]) < 0.01f &&
			MathF.Abs((float)spot.Attribute("y")! - position[1]) < 0.01f &&
			MathF.Abs((float)spot.Attribute("z")! - position[2]) < 0.01f, $"NPC {npcId} is not spawned at the contract's position.");
	}

	private static readonly Lazy<NaturalClassLineContract> Contract = new(NaturalClassLineContract.LoadDefault);
	private static readonly Lazy<XElement> QuestData = new(() => XDocument.Load(Data("quest_data", "quest_data.xml")).Root!);
	private static readonly Lazy<XElement> SkillTree = new(() => XDocument.Load(Data("skill_tree", "skill_tree.xml")).Root!);

	/// <summary>Every skill whose template carries a mastery effect, as SkillData indexes them for the equip check.</summary>
	private static readonly Lazy<Dictionary<int, (string Kind, string Unlocks)>> Masteries = new(() =>
	{
		var masteries = new Dictionary<int, (string, string)>();
		foreach (XElement template in Stream(Data("skills", "skill_templates.xml"), "skill_template"))
			foreach (XElement effect in template.Element("effects")?.Elements() ?? [])
			{
				(string, string)? mastery = effect.Name.LocalName switch
				{
					"wpnmastery" => ("weapon", (string)effect.Attribute("weapon")!),
					"armormastery" => ("armor", (string)effect.Attribute("armor")!),
					"shieldmastery" => ("shield", "SHIELD"),
					_ => null,
				};
				if (mastery != null)
					masteries[(int)template.Attribute("skill_id")!] = mastery.Value;
			}
		return masteries;
	});

	private static readonly Lazy<Dictionary<int, string?>> ItemGroups = new(() =>
		Stream(Data("items", "item_templates.xml"), "item_template")
			.ToDictionary(node => (int)node.Attribute("id")!, node => (string?)node.Attribute("item_group")));

	private static readonly Lazy<Dictionary<int, int>> TalkDistances = new(() =>
		Stream(Data("npcs", "npc_templates.xml"), "npc_template")
			.Where(node => node.Element("talk_info")?.Attribute("distance") != null)
			.ToDictionary(node => (int)node.Attribute("npc_id")!, node => (int)node.Element("talk_info")!.Attribute("distance")!));

	/// <summary>The named child elements of a large data file's root, one at a time.</summary>
	private static IEnumerable<XElement> Stream(string path, string elementName)
	{
		using XmlReader reader = XmlReader.Create(path, new XmlReaderSettings { IgnoreWhitespace = true, IgnoreComments = true });
		reader.MoveToContent();
		reader.Read();
		while (!reader.EOF)
		{
			if (reader.NodeType == XmlNodeType.Element && reader.Name == elementName)
				yield return (XElement)XNode.ReadFrom(reader);
			else
				reader.Read();
		}
	}

	private static XElement Quest(int id) => Assert.Single(QuestData.Value.Elements("quest"), quest => (int?)quest.Attribute("id") == id);
	private static int Int(Match match, int group) => int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);
	private static string Handler(string relative) => File.ReadAllText(Path.Combine(Root(), "src/Aion.GameServer/Handlers/Quest", relative));
	private static string Data(params string[] parts) => Path.Combine([Path.Combine(Root(), "game-server/data/static_data"), .. parts]);
	private static string Root() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
}
