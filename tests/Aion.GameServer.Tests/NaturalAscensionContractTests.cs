using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

/// <summary>NA-01: the Ascension bridge contract against shipped static data and the ported quest handlers.</summary>
public sealed class NaturalAscensionContractTests
{
	private static readonly Dictionary<int, string> SpawnFiles = new()
	{
		[220010000] = "Npcs/220010000_Ishalgen.xml",
		[120010000] = "Npcs/120010000_Pandaemonium.xml",
		[220030000] = "Npcs/220030000_Altgard.xml",
		[320020000] = "Instances/320020000_Ataxiar.xml",
	};

	[Fact]
	public void ContractStartsAtTheIshalgenStopAndPinsQuestDataAndTheEndpointLevel()
	{
		NaturalAscensionContract contract = NaturalAscensionContract.LoadDefault();
		Assert.Equal("ce54b7931546cddafb970d20c9f71fec6d48c83b", contract.JavaReference);

		using JsonDocument ishalgen = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "parity-artifacts/e2e", contract.Start.Contract)));
		JsonElement stop = ishalgen.RootElement.GetProperty("ascensionStop");
		JsonElement journey = ishalgen.RootElement.GetProperty("journey");
		Assert.Equal(stop.GetProperty("questId").GetInt32(), contract.Start.QuestId);
		Assert.Equal(stop.GetProperty("activationLevel").GetInt32(), contract.Start.Level);
		Assert.Equal(stop.GetProperty("status").GetString(), contract.Start.Status);
		Assert.Equal(stop.GetProperty("var").GetInt32(), contract.Start.Var);
		Assert.Equal(stop.GetProperty("firstObjectiveNpcId").GetInt32(), contract.Start.NpcId);
		Assert.Equal(journey.GetProperty("mapId").GetInt32(), contract.Start.MapId);
		Assert.Equal(journey.GetProperty("race").GetString(), contract.Start.Race);
		Assert.Equal(journey.GetProperty("initialClass").GetString(), contract.Start.Class);

		XElement questData = XDocument.Load(Data("quest_data", "quest_data.xml")).Root!;
		foreach (NaturalAscensionQuest expected in contract.Quests)
		{
			XElement quest = Quest(questData, expected.Id);
			Assert.Equal(expected.Category, (string?)quest.Attribute("category"));
			Assert.Equal(expected.MinimumLevel, (int?)quest.Attribute("minlevel_permitted"));
			Assert.Equal(contract.Start.Race, (string?)quest.Attribute("race_permitted"));
			Assert.All(quest.Elements("rewards"), reward => Assert.Equal(expected.RewardExperience, (int?)reward.Attribute("exp")));
			int[] finished = quest.Element("start_conditions")?.Elements("finished").Select(node => (int)node.Attribute("quest_id")!).ToArray() ?? [];
			Assert.Equal(expected.Prerequisite is { } prerequisite ? [prerequisite] : [], finished);
		}

		// Q2904: started by 2009's reward group 3, for the Priest-born classes, with a work item nobody hands out.
		XElement dispatch = Quest(questData, contract.Dispatch.QuestId);
		XElement condition = Assert.Single(dispatch.Element("start_conditions")!.Elements("finished"));
		Assert.Equal(contract.Dispatch.StartsAfter, (int?)condition.Attribute("quest_id"));
		Assert.Equal(contract.Dispatch.StartReward, (int?)condition.Attribute("reward"));
		Assert.Equal(contract.Dispatch.ClassesPermitted, ((string)dispatch.Element("class_permitted")!).Split(' '));
		Assert.Equal(contract.Dispatch.UnhandedWorkItemId, (int?)dispatch.Descendants("quest_work_item").Single().Attribute("item_id"));
		Assert.Equal(contract.CeremonyReward.RewardGroup, contract.Dispatch.StartReward);

		// Q2009: REWARD2 of the Priest list is the Karmic Staff; every group pays kinah and tea.
		XElement ceremony = Quest(questData, contract.CeremonyReward.QuestId);
		Assert.Equal("1", (string?)ceremony.Attribute("use_class_reward"));
		int[] selectable = ceremony.Elements(contract.CeremonyReward.SelectableList).Select(node => (int)node.Attribute("item_id")!).ToArray();
		int rewardIndex = NaturalAscensionContract.DialogActionId(contract.CeremonyReward.Action) - DialogAction.SELECTED_QUEST_REWARD1;
		Assert.Equal(contract.CeremonyReward.ItemId, selectable[rewardIndex]);
		Assert.All(ceremony.Elements("rewards"), reward =>
		{
			Assert.Contains(reward.Elements("reward_item"), item => (int?)item.Attribute("item_id") == 182400001 &&
				(long?)item.Attribute("count") == contract.CeremonyReward.Kinah);
			Assert.Contains(reward.Elements("reward_item"), item => (int?)item.Attribute("item_id") == contract.CeremonyReward.TeaItemId &&
				(int?)item.Attribute("count") == contract.CeremonyReward.TeaCount);
		});
		Assert.Equal(contract.CeremonyReward.ItemGroup, (string?)Item(contract.CeremonyReward.ItemId).Attribute("item_group"));
		Assert.Equal(contract.CeremonyReward.ItemId, Assert.Single(contract.Endpoint.EquippedItemIds));

		// Only one XP fact gates the route: a non-Daeva is capped at the start of level 10 (a full level 9 bar),
		// so level 10, and with it Q2904's gate, arrives with the Q2009 payout. Past Ascension there is no cap.
		long[] levels = XDocument.Load(Data("player_experience_table.xml")).Root!.Elements("exp").Select(node => (long)node).ToArray();
		Assert.Equal(levels[9], contract.Start.ExperienceCap);
		int ceremonyExperience = contract.Quests.Single(quest => quest.Id == contract.CeremonyReward.QuestId).RewardExperience;
		Assert.True(levels.Count(threshold => contract.Start.ExperienceCap + ceremonyExperience >= threshold) >= contract.Endpoint.MinimumLevel);
		Assert.Equal(contract.Endpoint.MinimumLevel, contract.Quests.Single(quest => quest.Id == contract.Dispatch.QuestId).MinimumLevel);
		Assert.Equal(contract.Quests.Select(quest => quest.Id), contract.Endpoint.CompletedQuestIds);
		Assert.Equal((int)PlayerClass.CLERIC, contract.Endpoint.ClassId);
		Assert.Equal(contract.ClassChoice.ToClass, contract.Endpoint.Class);
	}

	[Fact]
	public void StepsMatchSpawnsTalkRangesDialogsTeleportsAndMovies()
	{
		NaturalAscensionContract contract = NaturalAscensionContract.LoadDefault();
		XElement templates = XDocument.Load(Data("npcs", "npc_templates.xml")).Root!;
		string ascension = Handler("ascension/_2008Ascension.cs");
		string ceremony = Handler("ascension/_2009ACeremonyinPandaemonium.cs");
		string dispatch = Handler("ascension/_2904DispatchtoAltgard.cs");
		string suthran = Handler("altgard/_24010SuthransOrders.cs");
		var handlers = new Dictionary<int, string> { [2008] = ascension, [2009] = ceremony, [2904] = dispatch, [24010] = suthran };

		foreach (NaturalAscensionStep step in contract.Steps)
		{
			string handler = handlers[step.QuestId];
			Assert.Contains(step.NpcId.ToString(CultureInfo.InvariantCulture), handler, StringComparison.Ordinal);
			XElement template = Assert.Single(templates.Elements("npc_template"), node => (int?)node.Attribute("npc_id") == step.NpcId);
			Assert.Equal(step.TalkRange, (int)template.Element("talk_info")!.Attribute("distance")! + 1);

			bool spawnedByHandler = step.MapId == contract.Instance.MapId && step.NpcId == contract.Start.NpcId;
			if (spawnedByHandler)
				Assert.Contains($"Spawn({step.NpcId}, player, 301.92999f, 274.26001f, 205.7f", ascension, StringComparison.Ordinal);
			else
				AssertSpawn(step.MapId, step.NpcId, step.Position);

			// Talk and reward selections are handled generically; every other action is a branch in the handler.
			foreach (string action in step.Actions.Where(action => action != "USE_OBJECT" && !action.Contains("REWARD", StringComparison.Ordinal)))
				Assert.Contains($"DialogAction.{action}", handler, StringComparison.Ordinal);
			if (step.MovieId is { } movie)
				Assert.Contains($"PlayQuestMovie(env, {movie})", handler, StringComparison.Ordinal);
			if (step.ReceivesItemId is { } item)
				Assert.Contains($"GiveQuestItem(env, {item}, 1)", handler, StringComparison.Ordinal);
			if (step.Teleport is { } teleport)
			{
				string target = teleport.MapId == contract.Instance.MapId ? "newInstance" : teleport.MapId.ToString(CultureInfo.InvariantCulture);
				Assert.Contains(System.Text.RegularExpressions.Regex.Matches(handler,
					$@"TeleportTo\(player, {target}, ([\d.]+)f, ([\d.]+)f, ([\d.]+)f"), call =>
						Enumerable.Range(0, 3).All(axis => MathF.Abs(float.Parse(call.Groups[axis + 1].Value, CultureInfo.InvariantCulture) -
							teleport.Position[axis]) < 0.001f));
			}
		}

		Assert.Contains("PlayQuestMovie(env, 152)", ascension, StringComparison.Ordinal);
		Assert.Equal(contract.Movies.Order(), contract.Steps.Where(step => step.MovieId != null).Select(step => step.MovieId!.Value).Append(152).Order());

		// Class choice: the Asmodian Priest page, and SETPRO14 is Cleric.
		Assert.Equal(contract.ClassChoice.ClassPageId, ClassChangeService.GetClassSelectionDialogPageId(Race.ASMODIANS, PlayerClass.PRIEST));
		Assert.Equal(DialogAction.SETPRO14, NaturalAscensionContract.DialogActionId(contract.ClassChoice.Action));
		Assert.Matches($@"case DialogAction\.{contract.ClassChoice.Action}:\s*return var == 6 && SetPlayerClass\(env, qs, PlayerClass\.{contract.ClassChoice.ToClass}\);", ascension);
		XElement tree = XDocument.Load(Data("skill_tree", "skill_tree.xml")).Root!;
		foreach (int skill in contract.ClassChoice.MasterySkillIds)
			Assert.Contains(tree.Elements(), node => (int?)node.Attribute("skillId") == skill &&
				(string?)node.Attribute("classId") == contract.ClassChoice.ToClass && (int?)node.Attribute("minLevel") == 9);

		// Only NOREWARD leaves the instance: the return teleport sits in the SELECTED_QUEST_NOREWARD branch.
		NaturalAscensionStep exit = Assert.Single(contract.Steps, step => step.QuestId == 2008 && step.ExpectedStatus == "REWARD");
		Assert.Equal([contract.Instance.ExitAction], exit.Actions);
		string rewardBranch = ascension[ascension.IndexOf("DialogAction.SELECTED_QUEST_NOREWARD", StringComparison.Ordinal)..];
		Assert.True(rewardBranch.IndexOf("TeleportTo(player, 220010000, 386.03476f", StringComparison.Ordinal) is >= 0 and < 600);
	}

	[Fact]
	public void InstanceTeleporterBindShopAndItemsMatchShippedData()
	{
		NaturalAscensionContract contract = NaturalAscensionContract.LoadDefault();
		string ascension = Handler("ascension/_2008Ascension.cs");

		XElement map = Assert.Single(XDocument.Load(Data("world_maps.xml")).Root!.Elements("map"), node => (int?)node.Attribute("id") == contract.Instance.MapId);
		Assert.Equal("true", (string?)map.Attribute("instance"));
		Assert.Equal("NONE", (string?)map.Attribute("drop_type"));
		XElement fly = Assert.Single(XDocument.Load(Data("flypath_template.xml")).Root!.Elements("flypath_location"),
			node => (int?)node.Attribute("id") == contract.Instance.FlyPathId);
		Assert.Equal(contract.Instance.MapId, (int?)fly.Attribute("sworld"));
		Assert.Equal(contract.Instance.MapId, (int?)fly.Attribute("eworld"));
		Assert.Equal(contract.Instance.FlightSeconds, (int?)fly.Attribute("time"));
		Assert.Equal(contract.Instance.FlyPathEnd, new[] { (float)fly.Attribute("ex")!, (float)fly.Attribute("ey")!, (float)fly.Attribute("ez")! });
		Assert.Equal(contract.Instance.FlyPathId, Assert.Single(contract.Steps, step => step.FlyPathId != null).FlyPathId);
		foreach (NaturalAscensionTrialGroup group in contract.Instance.Trial)
		{
			Assert.Equal(group.Count, group.Positions.Length);
			foreach (float[] position in group.Positions)
				Assert.Contains($"Spawn({group.NpcId}, player, {string.Join(", ", position.Select(value => value.ToString(CultureInfo.InvariantCulture) + "f"))}",
					ascension, StringComparison.Ordinal);
		}
		string ai = File.ReadAllText(Directory.EnumerateFiles(Path.Combine(Root(), "src"), "AscensationNpcAI.cs", SearchOption.AllDirectories).Single());
		Assert.Contains($"return {contract.Instance.TrialDamage};", ai, StringComparison.Ordinal);
		foreach (int hostile in contract.Instance.HostileNpcIds)
			Assert.Contains(Spawns(contract.Instance.MapId).Descendants("spawn"), spawn => (int?)spawn.Attribute("npc_id") == hostile);
		Assert.Contains($"qs.SetQuestVar({contract.Instance.ResetVar})", ascension, StringComparison.Ordinal);

		XElement teleporter = Assert.Single(XDocument.Load(Data("npc_teleporter.xml")).Root!.Elements("teleporter_template"),
			node => (int?)node.Attribute("teleportId") == contract.Teleporter.TeleportId);
		Assert.Equal(contract.Teleporter.NpcId.ToString(CultureInfo.InvariantCulture), (string?)teleporter.Attribute("npc_ids"));
		XElement location = Assert.Single(teleporter.Descendants("telelocation"), node => (int?)node.Attribute("loc_id") == contract.Teleporter.LocationId);
		Assert.Equal(contract.Teleporter.BasePrice, (int?)location.Attribute("price"));
		Assert.Null(location.Attribute("required_quest"));
		XElement destination = Assert.Single(XDocument.Load(Data("teleport_location.xml")).Root!.Elements("teleloc_template"),
			node => (int?)node.Attribute("loc_id") == contract.Teleporter.LocationId);
		Assert.Equal(contract.Teleporter.Destination.MapId, (int?)destination.Attribute("mapid"));
		Assert.Equal(contract.Teleporter.Destination.Position,
			new[] { (float)destination.Attribute("posX")!, (float)destination.Attribute("posY")!, (float)destination.Attribute("posZ")! });

		XElement bind = Assert.Single(XDocument.Load(Data("bind_points", "bind_points.xml")).Root!.Elements("bind_point"),
			node => (int?)node.Attribute("npcid") == contract.Bind.NpcId);
		Assert.Equal(contract.Bind.Price, (int?)bind.Attribute("price"));
		AssertSpawn(contract.Bind.MapId, contract.Bind.NpcId, contract.Bind.Position);
		Assert.Equal(contract.Endpoint.BindNpcId, contract.Bind.NpcId);
		Assert.Equal(contract.Endpoint.MapId, contract.Bind.MapId);

		XElement trade = XDocument.Load(Data("npc_trade_list.xml")).Root!;
		XElement goods = XDocument.Load(Data("goodslists", "goodslists.xml")).Root!;
		int[] Lists(int npc) => Assert.Single(trade.Elements("tradelist_template"), node => (int?)node.Attribute("npc_id") == npc)
			.Elements("tradelist").Select(node => (int)node.Attribute("id")!).ToArray();
		bool Sells(int npc, int item) => Lists(npc).Any(list => goods.Elements("list")
			.Any(node => (int?)node.Attribute("id") == list && node.Elements("item").Any(entry => (int?)entry.Attribute("id") == item)));
		Assert.Contains(contract.Shop.PotionGoodsList, Lists(contract.Shop.PotionNpcId));
		Assert.False(contract.Shop.BuysGear);
		foreach (NaturalAscensionPurchase purchase in contract.Shop.Purchases)
			Assert.True(Sells(contract.Shop.PotionNpcId, purchase.ItemId) || Sells(contract.Shop.ReagentNpcId, purchase.ItemId), $"{purchase.ItemId} is not sold");
		foreach (int npc in new[] { contract.Shop.SellNpcId, contract.Shop.PotionNpcId, contract.Shop.ReagentNpcId })
		{
			Assert.Contains(Spawns(contract.Endpoint.MapId).Descendants("spawn"), spawn => (int?)spawn.Attribute("npc_id") == npc);
			string dialogs = (string)Assert.Single(XDocument.Load(Data("npcs", "npc_templates.xml")).Root!.Elements("npc_template"),
				node => (int?)node.Attribute("npc_id") == npc).Element("talk_info")!.Attribute("func_dialogs")!;
			Assert.Contains("2", dialogs.Split(' '));
			Assert.Contains("3", dialogs.Split(' '));
		}

		Assert.Equal(["RING", "NECKLACE", "BELT"], contract.KeptAccessories.Select(id => (string?)Item(id).Attribute("item_group")));
		Assert.All(contract.ProtectedItemIds, id => Item(id));
	}

	[Fact]
	public void ContractJavaReferenceMatchesThePortState()
	{
		using JsonDocument state = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "docs/upstream-port-state.json")));
		Assert.Equal(NaturalAscensionContract.LoadDefault().JavaReference, state.RootElement.GetProperty("lastCompletedJavaCommit").GetString());
	}

	private static void AssertSpawn(int mapId, int npcId, float[] position) =>
		Assert.Contains(Spawns(mapId).Descendants("spawn").Where(spawn => (int?)spawn.Attribute("npc_id") == npcId).Elements("spot"), spot =>
			MathF.Abs((float)spot.Attribute("x")! - position[0]) < 0.01f &&
			MathF.Abs((float)spot.Attribute("y")! - position[1]) < 0.01f &&
			MathF.Abs((float)spot.Attribute("z")! - position[2]) < 0.01f);

	private static XElement Spawns(int mapId) => XDocument.Load(Data("spawns", SpawnFiles[mapId])).Root!;

	private static XElement Item(int id)
	{
		foreach (XElement node in ItemTemplates.Value.Elements("item_template"))
			if ((int?)node.Attribute("id") == id)
				return node;
		throw new Xunit.Sdk.XunitException($"Item {id} is not shipped.");
	}

	private static readonly Lazy<XElement> ItemTemplates = new(() => XDocument.Load(Data("items", "item_templates.xml")).Root!);

	private static XElement Quest(XElement root, int id) => Assert.Single(root.Elements("quest"), quest => (int?)quest.Attribute("id") == id);
	private static string Handler(string relative) => File.ReadAllText(Path.Combine(Root(), "src/Aion.GameServer/Handlers/Quest", relative));
	private static string Data(params string[] parts) => Path.Combine([Path.Combine(Root(), "game-server/data/static_data"), .. parts]);
	private static string Root() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
}
