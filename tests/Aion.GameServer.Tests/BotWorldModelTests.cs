using System.Text.Json;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotWorldModelTests
{
	[Fact]
	public void RecipesTrackSnapshotLearningAndQuestCleanup()
	{
		var world = new BotWorldModel();
		world.Apply(Packet<SM_RECIPE_LIST>(("recipeIds", new[] { 155001381 })));
		world.Apply(Packet<SM_LEARN_RECIPE>(("recipeId", 155004206)));
		world.Apply(Packet<SM_LEARN_RECIPE>(("recipeId", 155004206)));
		Assert.Equal(new[] { 155001381, 155004206 }, world.Recipes.Order());
		world.Apply(Packet<SM_RECIPE_DELETE>(("recipeId", 155004206)));
		Assert.Equal(155001381, Assert.Single(world.Recipes));
		world.BeginWorldReload();
		Assert.Equal(155001381, Assert.Single(world.Recipes));
		world.Apply(Packet<SM_RECIPE_LIST>(("recipeIds", Array.Empty<int>())));
		Assert.Empty(world.Recipes);
	}

	[Fact]
	public void VendorPricesUseObservedRatesAndTruncateEachStage()
	{
		var world = new BotWorldModel();
		world.Apply(Packet<SM_PRICES>(("globalPrices", (byte)117), ("globalModifier", (byte)109), ("taxes", (byte)113)));
		Assert.NotNull(world.VendorPrices);
		Assert.Equal(178, world.VendorPrices.BuyPrice(101, 123));
		Assert.Equal(20, BotVendorPrices.SellPrice(101, 20));
		world.Apply(Packet<SM_PRICES>(("globalPrices", (byte)100), ("globalModifier", (byte)100), ("taxes", (byte)100)));
		Assert.Equal(124, world.VendorPrices.BuyPrice(101, 123));
	}

	[Fact]
	public void BeginWorldReloadClearsTransientVisibleState()
	{
		var world = new BotWorldModel();
		world.Apply(Packet<SM_NPC_INFO>(
			("objectId", 200), ("npcId", 203500), ("visualNpcId", 203500),
			("x", 1f), ("y", 2f), ("z", 3f), ("heading", (byte)4),
			("creatureType", (byte)38)));
		world.Apply(Packet<SM_DIALOG_WINDOW>(("targetObjectId", 200), ("dialogPageId", (ushort)1011), ("questId", 2101)));
		world.Apply(Packet<SM_QUESTION_WINDOW>(
			("code", 1), ("params", Array.Empty<string>()), ("senderId", 200), ("rangeOrCooldownSeconds", 0)));
		world.Apply(Packet<SM_LOOT_STATUS>(("targetObjectId", 200), ("status", (byte)2), ("lootEffectId", 0)));
		world.Apply(Packet<SM_TRADELIST>(
			("targetObjectId", 200), ("tradeNpcType", (byte)1), ("buyPriceModifier", 100),
			("showBuyTab", true), ("showSellTab", false), ("tabs", Array.Empty<int>()),
			("limitedItems", new List<IReadOnlyDictionary<string, object?>>())));

		world.BeginWorldReload();

		Assert.Empty(world.Objects);
		Assert.Null(world.Dialog);
		Assert.Null(world.Question);
		Assert.Null(world.Loot);
		Assert.Null(world.Trade);
	}

	[Fact]
	public void TracksKnownObjectsAndSelfStateFromDecodedPackets()
	{
		var world = new BotWorldModel();
		world.Apply(Packet<SM_PLAYER_INFO>(
			("x", 1f), ("y", 2f), ("z", 3f), ("heading", (byte)4), ("objectId", 100),
			("race", (byte)0), ("playerClass", (byte)1), ("state", (ushort)2), ("name", "Daeva"),
			("movementSpeed", 6f)));
		world.Apply(Packet<SM_NPC_INFO>(
			("x", 10f), ("y", 20f), ("z", 30f), ("objectId", 200), ("npcId", 700001),
			("visualNpcId", 700002), ("creatureType", (byte)1)));
		world.Apply(Packet<SM_GATHERABLE_INFO>(
			("x", 11f), ("y", 21f), ("z", 31f), ("heading", (byte)8), ("objectId", 300),
			("staticId", 70), ("templateId", 400001), ("objectState", (ushort)1), ("isStatic", false), ("open", null)));
		world.Apply(Packet<SM_GATHERABLE_INFO>(
			("x", 12f), ("y", 22f), ("z", 32f), ("heading", (byte)9), ("objectId", 301),
			("staticId", 71), ("templateId", 400002), ("objectState", (ushort)9), ("isStatic", true), ("open", true)));

		world.Apply(Packet<SM_STATS_INFO>(
			("objectId", 100), ("level", (ushort)12), ("expNeeded", 900L), ("expRecoverable", 4L), ("expShown", 500L),
			("maxHp", 1200), ("currentHp", 1100), ("maxMp", 800), ("currentMp", 700),
			("maxDp", (ushort)4000), ("dp", (ushort)250), ("maxFp", 60), ("currentFp", 55)));
		world.Apply(Packet<SM_EMOTION>(
			("senderObjectId", 100), ("emotionType", (byte)EmotionType.CHANGE_SPEED),
			("state", (ushort)2), ("movementSpeed", 4.5f)));
		world.Apply(Packet<SM_PLAYER_SPAWN>(
			("worldId", 210010000), ("x", 5f), ("y", 6f), ("z", 7f), ("heading", (byte)10)));
		world.Apply(Packet<SM_MOVE>(
			("objectId", 100), ("x", 15f), ("y", 16f), ("z", 17f), ("heading", (byte)11), ("movementMask", (byte)0)));
		world.Apply(Packet<SM_STATUPDATE_HP>(("currentHp", 1000), ("maxHp", 1300)));
		world.Apply(Packet<SM_STATUPDATE_MP>(("currentMp", 600), ("maxMp", 900)));
		world.Apply(Packet<SM_STATUPDATE_DP>(("currentDp", (ushort)300)));
		world.Apply(Packet<SM_STATUPDATE_EXP>(
			("currentExp", 700L), ("recoverableExp", 5L), ("maxExp", 1000L), ("rep1", 0L), ("rep2", 0L)));
		world.Apply(Packet<SM_FLY_TIME>(("currentFp", 40), ("maxFp", 70)));

		Assert.Equal(BotKnownObjectKind.Player, world.Objects[100].Kind);
		Assert.Equal(new BotPosition(15, 16, 17, 11), world.Position);
		Assert.Equal(BotKnownObjectKind.Npc, world.Objects[200].Kind);
		Assert.Equal(BotKnownObjectKind.Gatherable, world.Objects[300].Kind);
		Assert.Equal(BotKnownObjectKind.Static, world.Objects[301].Kind);
		Assert.True(world.Objects[301].IsOpen);
		Assert.Equal(210010000, world.MapId);
		Assert.Equal((ushort)12, world.Level);
		Assert.Equal(1000, world.CurrentHp);
		Assert.Equal(1300, world.MaxHp);
		Assert.Equal(600, world.CurrentMp);
		Assert.Equal(900, world.MaxMp);
		Assert.Equal((ushort)300, world.CurrentDp);
		Assert.Equal(700L, world.CurrentExperience);
		Assert.Equal(5L, world.RecoverableExperience);
		Assert.Equal(1000L, world.ExperienceNeeded);
		Assert.Equal(40, world.CurrentFlightTime);
		Assert.Equal(70, world.MaxFlightTime);
		Assert.Equal(4.5f, world.MovementSpeed);
		Assert.Equal(4.5f, world.Objects[100].MovementSpeed);

		world.Apply(Packet<SM_DIE>(
			("allowReviveBySkill", true), ("allowReviveByItem", false), ("remainingKiskTimeSeconds", 30),
			("allowInstanceRevive", true), ("invasion", false)));
		Assert.True(world.IsDead);
		Assert.True(world.ReviveOptions!.BySkill);
		world.Apply(Packet<SM_STATUPDATE_HP>(("currentHp", 500), ("maxHp", 1300)));
		Assert.False(world.IsDead);
		Assert.Null(world.ReviveOptions);

		world.Apply(Packet<SM_DELETE>(("objectId", 200), ("animationId", (byte)0)));
		Assert.DoesNotContain(200, world.Objects);
	}

	[Fact]
	public void TracksInventoryKinahSkillsCooldownsAndQuestStates()
	{
		var world = new BotWorldModel();
		world.Apply(Packet<SM_INVENTORY_INFO>(
			("firstPacket", true),
			("items", Items(
				Item(("objectId", 1), ("itemId", BotWorldModel.KinahItemId), ("desc", "Kinah"), ("itemCount", 1234L),
					("itemMask", (ushort)0), ("itemCreator", ""), ("equipmentSlot", ushort.MaxValue), ("cloth", false)),
				Item(("objectId", 2), ("itemId", 160000001), ("desc", "Potion"), ("itemCount", 7L),
					("itemMask", (ushort)3), ("itemCreator", "Daeva"), ("equipmentSlot", ushort.MaxValue), ("cloth", false))))));
		Assert.Equal(1234L, world.Kinah);
		Assert.Equal(7L, world.Inventory[2].Count);

		world.Apply(Packet<SM_INVENTORY_UPDATE_ITEM>(
			("objectId", 2), ("desc", "Potion+"), ("itemCount", 5L), ("itemMask", (ushort)4), ("itemCreator", "Daeva")));
		Assert.Equal(5L, world.Inventory[2].Count);
		Assert.Equal("Potion+", world.Inventory[2].Description);
		world.Apply(Packet<SM_INVENTORY_UPDATE_ITEM>(
			("objectId", 2), ("desc", "Potion+"), ("itemCount", null), ("itemMask", null), ("itemCreator", null)));
		Assert.Equal(5L, world.Inventory[2].Count);
		world.Apply(Packet<SM_DELETE_ITEM>(("itemObjectId", 1)));
		Assert.Equal(0L, world.Kinah);

		world.Apply(Packet<SM_SKILL_LIST>(
			("silentUpdate", true),
			("skills", Items(Item(("skillId", (ushort)101), ("level", (ushort)2), ("reserved", (byte)0),
				("professionBarSize", (byte)0), ("flag", 3), ("skillType", (byte)1))))));
		world.Apply(Packet<SM_SKILL_LIST>(
			("silentUpdate", false),
			("skills", Items(Item(("skillId", (ushort)102), ("level", (ushort)1), ("reserved", (byte)0),
				("professionBarSize", (byte)0), ("flag", 0), ("skillType", (byte)2))))));
		Assert.Equal(2, world.Skills.Count);
		world.Apply(Packet<SM_SKILL_COOLDOWN>(
			("cooldowns", Items(Item(("skillId", (ushort)101), ("remainingSeconds", 9), ("durationMillis", 12000))))));
		Assert.Equal(9, world.Cooldowns[101].RemainingSeconds);
		world.Apply(Packet<SM_SKILL_COOLDOWN>(
			("cooldowns", Items(Item(("skillId", (ushort)101), ("remainingSeconds", 0), ("durationMillis", 12000))))));
		Assert.Empty(world.Cooldowns);

		world.Apply(Packet<SM_QUEST_LIST>(
			("quests", Items(Item(("questId", 1001), ("status", (byte)3), ("stepAndFlags", 2), ("completeCount", (byte)0))))));
		world.Apply(Packet<SM_QUEST_ACTION>(
			("action", (byte)2), ("questId", 1001), ("status", (byte)5), ("stepAndFlags", 3)));
		world.Apply(Packet<SM_QUEST_ACTION>(("action", (byte)4), ("questId", 1001), ("timer", 60)));
		Assert.Equal(3, world.Quests[1001].StepAndFlags);
		Assert.Equal(60, world.Quests[1001].TimerSeconds);
		world.Apply(Packet<SM_QUEST_ACTION>(
			("action", (byte)5), ("questId", 1002), ("sharerId", 77), ("shareInAlliance", true)));
		Assert.True(world.PendingQuestShare!.InAlliance);

		world.Apply(Packet<SM_QUEST_COMPLETED_LIST>(
			("updateMode", (byte)0),
			("quests", Items(Item(("questId", 900), ("completeCount", (byte)1), ("nonRepeatable", true))))));
		Assert.True(world.CompletedQuests[900].NonRepeatable);
		Assert.Equal([900, 1001], world.AcceptedQuestIds.Order());
		Assert.Equal([900, 1001], world.CompletedQuestIds.Order());
		world.Apply(Packet<SM_QUEST_ACTION>(("action", (byte)3), ("questId", 1001)));
		Assert.Empty(world.Quests);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void TracksOpenWindowsAndSystemMessagesByStrName(bool listBeforeOpen)
	{
		var world = new BotWorldModel();
		world.Apply(Packet<SM_DIALOG_WINDOW>(("targetObjectId", 10), ("dialogPageId", (ushort)1011), ("questId", 1001)));
		world.Apply(Packet<SM_QUESTION_WINDOW>(
			("code", 6), ("params", new[] { "a", "b", "c" }), ("senderId", 20), ("rangeOrCooldownSeconds", 30)));
		if (!listBeforeOpen)
			world.Apply(Packet<SM_LOOT_STATUS>(("targetObjectId", 30), ("status", (byte)2), ("lootEffectId", 0)));
		world.Apply(Packet<SM_LOOT_ITEMLIST>(
			("targetObjectId", 30),
			("items", Items(Item(("index", (byte)1), ("itemId", 160000001), ("count", 2), ("requiresConfirmation", false))))));
		if (listBeforeOpen)
			world.Apply(Packet<SM_LOOT_STATUS>(("targetObjectId", 30), ("status", (byte)2), ("lootEffectId", 0)));
		world.Apply(Packet<SM_TRADELIST>(
			("targetObjectId", 40), ("tradeNpcType", (byte)1), ("buyPriceModifier", 100),
			("showBuyTab", true), ("showSellTab", false), ("tabs", new[] { 100, 101 }),
			("limitedItems", Items(Item(("itemId", 160000002), ("buyCount", (ushort)2), ("sellLimit", (ushort)5))))));
		world.Apply(Packet<SM_SYSTEM_MESSAGE>(
			("msgId", 901354), ("name", "STR_MOVE_PORTAL_ERROR_INVALID_RACE"),
			("params", new[] { "Daeva" }), ("specialParams", Array.Empty<string>()), ("senderObjectId", 100)));

		Assert.Equal((ushort)1011, world.Dialog!.PageId);
		Assert.Equal(6, world.Question!.Code);
		Assert.Equal(160000001, Assert.Single(world.Loot!.Items).ItemId);
		Assert.Equal(new[] { 100, 101 }, world.Trade!.Tabs);
		Assert.Equal("STR_MOVE_PORTAL_ERROR_INVALID_RACE", Assert.Single(world.SystemMessages).Name);

		world.Apply(Packet<SM_CLOSE_QUESTION_WINDOW>());
		world.Apply(Packet<SM_LOOT_STATUS>(("targetObjectId", 30), ("status", (byte)3), ("lootEffectId", 0)));
		world.Apply(Packet<SM_DIALOG_WINDOW>(("targetObjectId", 10), ("dialogPageId", (ushort)0), ("questId", 0)));
		Assert.Null(world.Question);
		Assert.Null(world.Loot);
		Assert.Null(world.Dialog);
		Assert.Null(world.Trade);
	}

	[Fact]
	public void QuestCoverageReceiptPersistsObservedHistoryAndProblems()
	{
		var world = new BotWorldModel();
		world.Apply(Packet<SM_QUEST_ACTION>(
			("action", (byte)2), ("questId", 1100), ("status", (byte)3), ("stepAndFlags", 0)));
		world.Apply(Packet<SM_QUEST_ACTION>(
			("action", (byte)2), ("questId", 1101), ("status", (byte)5), ("stepAndFlags", 0)));
		string root = Path.Combine(Path.GetTempPath(), $"quest-coverage-{Guid.NewGuid():N}");
		try
		{
			string path = QuestCoverageReceipt.Save(root, "sim", "q-test", world,
				[new QuestCoverageProblem(1100, "echo")],
				[new QuestCoverageProblem(1100, "needs item")]);
			using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
			JsonElement receipt = document.RootElement;
			Assert.Equal("SIM", receipt.GetProperty("mode").GetString());
			Assert.Equal([1100, 1101], receipt.GetProperty("acceptedQuestIds").EnumerateArray().Select(value => value.GetInt32()));
			Assert.Equal([1101], receipt.GetProperty("completedQuestIds").EnumerateArray().Select(value => value.GetInt32()));
			Assert.Equal("echo", receipt.GetProperty("echoFailures")[0].GetProperty("reason").GetString());
			Assert.Equal("needs item", receipt.GetProperty("stuckReasons")[0].GetProperty("reason").GetString());
		}
		finally
		{
			if (Directory.Exists(root))
				Directory.Delete(root, recursive: true);
		}
	}

	private static DecodedBotServerPacket Packet<T>(params (string Name, object? Value)[] fields) =>
		new(typeof(T), Item(fields));

	private static Dictionary<string, object?> Item(params (string Name, object? Value)[] fields) =>
		fields.ToDictionary(field => field.Name, field => field.Value, StringComparer.Ordinal);

	private static List<IReadOnlyDictionary<string, object?>> Items(params IReadOnlyDictionary<string, object?>[] items) =>
		items.ToList();
}
