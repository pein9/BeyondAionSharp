using System.Text.Json;
using System.Xml.Linq;
using Aion.Bots.Scenarios;

namespace Aion.GameServer.Tests;

public sealed class NaturalIshalgenContractTests
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	[Fact]
	public void ContractPinsReviewedQuestScopeGatesPrerequisitesAndExperience()
	{
		NaturalIshalgenContract contract = LoadContract();
		Assert.Equal(1, contract.SchemaVersion);
		Assert.Equal("ce54b7931546cddafb970d20c9f71fec6d48c83b", contract.JavaReference);
		Assert.Equal(220010000, contract.Journey.MapId);
		Assert.Equal("ASMODIANS", contract.Journey.Race);
		Assert.Equal("PRIEST", contract.Journey.InitialClass);
		Assert.Equal(41, contract.Journey.IncludedQuestCount);
		Assert.Equal(contract.Journey.IncludedQuestCount, contract.Quests.Length);
		Assert.Equal(contract.Quests.Length, contract.Quests.Select(quest => quest.Id).Distinct().Count());
		Assert.Equal(contract.Quests.Select(quest => quest.Id).Order(), contract.Quests.Select(quest => quest.Id));

		XElement questData = XDocument.Load(Data("quest_data", "quest_data.xml")).Root!;
		Dictionary<int, XElement> shipped = questData.Elements("quest")
			.ToDictionary(quest => (int)quest.Attribute("id")!);
		foreach (NaturalQuestContract expected in contract.Quests)
		{
			XElement actual = shipped[expected.Id];
			Assert.Equal("Ishalgen", (string?)actual.Attribute("quest_zone"));
			Assert.Equal(contract.Journey.Race, (string?)actual.Attribute("race_permitted"));
			Assert.Equal(expected.MinimumLevel, (int?)actual.Attribute("minlevel_permitted"));

			int[] experience = actual.Elements("rewards")
				.Select(reward => (int?)reward.Attribute("exp") ?? 0)
				.ToArray();
			Assert.NotEmpty(experience);
			Assert.Equal(expected.MinimumRewardExperience, experience.Min());
			Assert.Equal(expected.MaximumRewardExperience, experience.Max());

			int[] prerequisites = actual.Element("start_conditions")?.Elements("finished")
				.Select(condition => (int)condition.Attribute("quest_id")!)
				.Order()
				.ToArray() ?? [];
			Assert.Equal(expected.Prerequisites.Order(), prerequisites);
		}

		Assert.Equal(contract.RewardExperience.Minimum, contract.Quests.Sum(quest => quest.MinimumRewardExperience));
		Assert.Equal(contract.RewardExperience.Maximum, contract.Quests.Sum(quest => quest.MaximumRewardExperience));
		long[] levels = XDocument.Load(Data("player_experience_table.xml")).Root!.Elements("exp")
			.Select(node => (long)node)
			.ToArray();
		Assert.Equal(contract.LevelThresholds.Level9, levels[9]);
		Assert.Equal(contract.LevelThresholds.Level10, levels[10]);
		Assert.True(contract.RewardExperience.Minimum > contract.LevelThresholds.Level9);
		Assert.True(contract.RewardExperience.Maximum < contract.LevelThresholds.Level10);
	}

	[Fact]
	public void ContractPinsReviewedExclusionsAndStarterEvidence()
	{
		NaturalIshalgenContract contract = LoadContract();
		int[] included = contract.Quests.Select(quest => quest.Id).ToArray();
		int[] excluded = contract.Exclusions.SelectMany(group => group.QuestIds).ToArray();
		Assert.Equal(excluded.Length, excluded.Distinct().Count());
		Assert.Empty(included.Intersect(excluded));
		Assert.Equal(
			["disabledEvent", "minimumLevel99", "missingStarterSource", "noHandler", "postBoundary", "randomDropStarter", "randomDropStarter"],
			contract.Exclusions.Select(group => group.Reason).Order());

		NaturalQuestExclusion missingSource = Assert.Single(contract.Exclusions, group => group.Reason == "missingStarterSource");
		Assert.Equal([2107], missingSource.QuestIds);
		Assert.Equal(182203107, missingSource.StarterItemId);
		string[] staticOccurrences = Directory.EnumerateFiles(StaticData(), "*.xml", SearchOption.AllDirectories)
			.Where(path => File.ReadAllText(path).Contains("182203107", StringComparison.Ordinal))
			.Select(path => Path.GetRelativePath(Root(), path).Replace('\\', '/'))
			.Order()
			.ToArray();
		Assert.Equal(
			["game-server/data/static_data/items/item_templates.xml", "game-server/data/static_data/quest_data/quest_data.xml"],
			staticOccurrences);
		Assert.DoesNotContain(
			Directory.EnumerateFiles(Path.Combine(Root(), "src"), "*.cs", SearchOption.AllDirectories),
			path => File.ReadAllText(path).Contains("182203107", StringComparison.Ordinal));

		XElement drops = XDocument.Load(Data("global_drops", "rules", "open_worlds", "rules_map_ishalgen.xml")).Root!;
		foreach (NaturalQuestExclusion random in contract.Exclusions.Where(group => group.Reason == "randomDropStarter"))
		{
			XElement rule = Assert.Single(drops.Elements("gd_rule"), candidate =>
				candidate.Descendants("gd_item").Any(item => (int?)item.Attribute("id") == random.StarterItemId));
			Assert.Equal(random.DropChancePercent, (int?)rule.Attribute("chance"));
			Assert.Contains(rule.Descendants("gd_map"), map => (int?)map.Attribute("map_id") == contract.Journey.MapId);
			Assert.Equal(random.SourceNpcIds.Order(), rule.Descendants("gd_npc").Select(npc => (int)npc.Attribute("npc_id")!).Order());
		}

		XElement questData = XDocument.Load(Data("quest_data", "quest_data.xml")).Root!;
		foreach (int id in contract.Exclusions.Single(group => group.Reason == "minimumLevel99").QuestIds)
			Assert.Equal(99, (int?)Quest(questData, id).Attribute("minlevel_permitted"));
		foreach (int id in contract.Exclusions.Single(group => group.Reason == "disabledEvent").QuestIds)
			Assert.Contains("Event", (string?)Quest(questData, id).Attribute("name"));
		Assert.Equal([10, 10, 16, 16], contract.Exclusions.Single(group => group.Reason == "postBoundary").QuestIds
			.Select(id => (int)Quest(questData, id).Attribute("minlevel_permitted")!));

		XElement scripts = XDocument.Load(Data("quest_script_data", "ishalgen.xml")).Root!;
		foreach (int id in contract.Exclusions.Single(group => group.Reason == "noHandler").QuestIds)
			Assert.DoesNotContain(scripts.Descendants(), element => (int?)element.Attribute("id") == id);
	}

	[Fact]
	public void ContractPinsNaturalGatheringAndUntouchedAscensionStop()
	{
		NaturalIshalgenContract contract = LoadContract();
		XElement questData = XDocument.Load(Data("quest_data", "quest_data.xml")).Root!;
		XElement gatheringQuest = Quest(questData, contract.Gathering.QuestId);
		XElement collect = Assert.Single(gatheringQuest.Element("collect_items")!.Elements("collect_item"));
		Assert.Equal(contract.Gathering.ItemId, (int?)collect.Attribute("item_id"));
		Assert.Equal(contract.Gathering.Count, (int?)collect.Attribute("count"));

		XElement templates = XDocument.Load(Data("gatherables", "gatherable_templates.xml")).Root!;
		XElement source = Assert.Single(templates.Elements("gatherable_template"), node =>
			(int?)node.Attribute("id") == contract.Gathering.SourceNpcId);
		Assert.Equal(contract.Gathering.SkillId, (int?)source.Attribute("harvestSkill"));
		Assert.Equal(contract.Gathering.SkillLevel, (int?)source.Attribute("skillLevel"));
		Assert.Contains(source.Descendants("material"), material => (int?)material.Attribute("itemid") == contract.Gathering.ItemId);
		XElement spawns = XDocument.Load(Data("spawns", "Gather", "220010000_Ishalgen.xml")).Root!;
		Assert.Contains(spawns.Descendants("spawn"), spawn =>
			(int?)spawn.Attribute("npc_id") == contract.Gathering.SourceNpcId && spawn.Elements("spot").Any());

		XElement ascension = Quest(questData, contract.AscensionStop.QuestId);
		Assert.Equal(contract.AscensionStop.ActivationLevel, (int?)ascension.Attribute("minlevel_permitted"));
		Assert.Equal(contract.Journey.Race, (string?)ascension.Attribute("race_permitted"));
		Assert.Equal("START", contract.AscensionStop.Status);
		Assert.Equal(0, contract.AscensionStop.Step);
		Assert.Equal(0, contract.AscensionStop.Var);
		Assert.False(contract.AscensionStop.SimpleSecondClassRequired);

		string handler = File.ReadAllText(Path.Combine(Root(), "src/Aion.GameServer/Handlers/Quest/ascension/_2008Ascension.cs"));
		Assert.Contains("if (CustomConfig.ENABLE_SIMPLE_2NDCLASS)", handler, StringComparison.Ordinal);
		Assert.Contains("qe.RegisterOnLevelChanged(questId)", handler, StringComparison.Ordinal);
		Assert.Contains("DefaultOnLevelChangedEvent(player)", handler, StringComparison.Ordinal);
		Assert.Contains($"qe.RegisterQuestNpc({contract.AscensionStop.FirstObjectiveNpcId})", handler, StringComparison.Ordinal);
		string baseHandler = File.ReadAllText(Path.Combine(Root(), "src/Aion.GameServer/QuestEngine/Handlers/AbstractQuestHandler.cs"));
		Assert.Contains("QuestService.AddOrUpdateQuest(player, questId, QuestStatus.START)", baseHandler, StringComparison.Ordinal);
		string config = File.ReadAllText(Path.Combine(Root(), "src/Aion.GameServer/Configs/Main/CustomConfig.cs"));
		Assert.Contains("ENABLE_SIMPLE_2NDCLASS = false", config, StringComparison.Ordinal);
	}

	[Fact]
	public void ContractJavaReferenceMatchesThePortState()
	{
		using JsonDocument state = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "docs/upstream-port-state.json")));
		Assert.Equal(LoadContract().JavaReference, state.RootElement.GetProperty("lastCompletedJavaCommit").GetString());
	}

	private static NaturalIshalgenContract LoadContract() => JsonSerializer.Deserialize<NaturalIshalgenContract>(
		File.ReadAllText(Path.Combine(Root(), "parity-artifacts/e2e/natural-ishalgen-contract.json")), JsonOptions)!;

	private static XElement Quest(XElement root, int id) => Assert.Single(
		root.Elements("quest"), quest => (int?)quest.Attribute("id") == id);

	private static string Data(params string[] parts) => Path.Combine([StaticData(), .. parts]);
	private static string StaticData() => Path.Combine(Root(), "game-server/data/static_data");
	private static string Root() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));

	private sealed class NaturalIshalgenContract
	{
		public int SchemaVersion { get; init; }
		public string JavaReference { get; init; } = "";
		public JourneyContract Journey { get; init; } = new();
		public LevelThresholdContract LevelThresholds { get; init; } = new();
		public RewardExperienceContract RewardExperience { get; init; } = new();
		public NaturalQuestContract[] Quests { get; init; } = [];
		public NaturalQuestExclusion[] Exclusions { get; init; } = [];
		public GatheringContract Gathering { get; init; } = new();
		public AscensionStopContract AscensionStop { get; init; } = new();
	}

	private sealed class JourneyContract
	{
		public int MapId { get; init; }
		public string Race { get; init; } = "";
		public string InitialClass { get; init; } = "";
		public int IncludedQuestCount { get; init; }
	}

	private sealed class LevelThresholdContract
	{
		public long Level9 { get; init; }
		public long Level10 { get; init; }
	}

	private sealed class RewardExperienceContract
	{
		public int Minimum { get; init; }
		public int Maximum { get; init; }
	}

	private sealed class NaturalQuestContract
	{
		public int Id { get; init; }
		public int MinimumLevel { get; init; }
		public int MinimumRewardExperience { get; init; }
		public int MaximumRewardExperience { get; init; }
		public int[] Prerequisites { get; init; } = [];
	}

	private sealed class NaturalQuestExclusion
	{
		public int[] QuestIds { get; init; } = [];
		public string Reason { get; init; } = "";
		public int? StarterItemId { get; init; }
		public int? DropChancePercent { get; init; }
		public int[] SourceNpcIds { get; init; } = [];
	}

	private sealed class GatheringContract
	{
		public int QuestId { get; init; }
		public int ItemId { get; init; }
		public int Count { get; init; }
		public int SourceNpcId { get; init; }
		public int SkillId { get; init; }
		public int SkillLevel { get; init; }
	}

	private sealed class AscensionStopContract
	{
		public int QuestId { get; init; }
		public int ActivationLevel { get; init; }
		public string Status { get; init; } = "";
		public int Step { get; init; }
		public int Var { get; init; }
		public int FirstObjectiveNpcId { get; init; }
		public bool SimpleSecondClassRequired { get; init; }
	}
}
