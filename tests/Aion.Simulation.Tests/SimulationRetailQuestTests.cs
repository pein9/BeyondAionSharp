using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// D32 (docs/retail-quest-completion.md): each quest that 4.8 retail ran and Java never implemented is played end to
	/// end from its compiled plan (parity-artifacts/e2e/retail-quest-plans/): accepted at its giver, its kills or items
	/// done, reported, and rewarded with what quest_data.xml says. Ascension and the level are fixture setup;
	/// most prerequisites are setup too, but Construction Basics earns Q2929 through its ordinary dialogs.
	/// Its profession/recipe/materials are setup; the Iron Clamps are produced through CM_CRAFT, not granted.
	/// the rest is the dialog, combat and loot protocol a player uses. The cases are the D32 register
	/// (parity-artifacts/e2e/retail-quest-implemented.json), which gives each quest its own fixture account.
	/// </summary>
	public static TheoryData<int, int, string> RetailQuestCases
	{
		get
		{
			var data = new TheoryData<int, int, string>();
			string path = Path.Combine(Aion.GameServer.TestKit.RealStaticData.RepoRoot(),
				"parity-artifacts", "e2e", "retail-quest-implemented.json");
			using var register = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
			foreach (System.Text.Json.JsonProperty quest in register.RootElement.GetProperty("quests").EnumerateObject())
			{
				data.Add(int.Parse(quest.Name, System.Globalization.CultureInfo.InvariantCulture),
					quest.Value.GetProperty("simAccount").GetInt32(), quest.Value.GetProperty("simCharacter").GetString()!);
			}
			return data;
		}
	}

	[SkippableTheory]
	[MemberData(nameof(RetailQuestCases))]
	public async Task RetailQuestPlaysEndToEnd(int questId, int accountId, string characterName)
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		QuestRunPlan plan = QuestRunPlan.Load(Path.Combine(Aion.GameServer.TestKit.RealStaticData.RepoRoot(),
			"parity-artifacts", "e2e", "retail-quest-plans", $"{questId}.json"));
		Race race = plan.Race == "ELYOS" ? Race.ELYOS : Race.ASMODIANS;
		using var policy = NewPolicy($"D32-{questId}", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(fixture, policy, "b01", accountId, characterName, race);

		session.BeginStep("s00", "login-create-enter-and-level");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		// Ascend first (it completes the ascension quest and makes a Daeva); a non-Daeva is capped at level 9.
		if (plan.MinimumLevel >= 10)
			Assert.True(ClassChangeService.SetClass(player, PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		player.GetCommonData().SetLevel(Math.Max(plan.MinimumLevel, 1));
		Assert.Equal(Math.Max(plan.MinimumLevel, 1), player.GetLevel());
		await session.DrainServerPacketsAsync(token);
		var driver = new SimulationQuestRunDriver(this, session, player);
		if (questId == 2929)
		{
			player.GetCommonData().SetLevel(9);
			Assert.False(QuestService.CheckStartConditions(player, questId, false));
			player.GetCommonData().SetLevel(10);
			Assert.True(QuestService.CheckStartConditions(player, questId, false));
			AssertArtisanFollowUpGates(player, available: false);
		}
		if (questId == 29049)
		{
			AssertArtisanFollowUpGates(player, available: false);
			QuestRunPlan introduction = QuestRunPlan.Load(Path.Combine(Aion.GameServer.TestKit.RealStaticData.RepoRoot(),
				"parity-artifacts", "e2e", "retail-quest-plans", "2929.json"));
			await QuestRunExecutor.ExecuteAsync(QuestRunBook.Build(introduction), driver, token);
			Assert.Equal(QuestStatus.COMPLETE, player.GetQuestStateList().GetQuestState(2929).GetStatus());
			AssertArtisanFollowUpGates(player, available: true);
		}
		long kinahBefore = player.GetInventory().GetKinah();
		long expBefore = player.GetCommonData().GetExp();

		session.BeginStep("s01", $"quest-plan:{plan.Template}:{plan.Name}");
		if (questId == 29049)
			await PlayConstructionBasicsAsync(session, player, plan, driver, token);
		else
			await QuestRunExecutor.ExecuteAsync(QuestRunBook.Build(plan), driver, token);

		QuestState state = player.GetQuestStateList().GetQuestState(questId);
		Assert.Equal(QuestStatus.COMPLETE, state.GetStatus());
		Assert.True(session.Api.World.CompletedQuests.ContainsKey(questId) ||
			session.Api.World.Quests.TryGetValue(questId, out BotQuestState? seen) && seen.Status == 5,
			$"Bot world did not observe Q{questId} complete.");
		Rewards rewards = DataManager.QUEST_DATA.GetQuestById(questId).GetRewards()[0];
		Assert.Equal(rewards.GetKinah(), player.GetInventory().GetKinah() - kinahBefore);
		if (questId == 2929)
		{
			Assert.Equal(rewards.GetExp(), player.GetCommonData().GetExp() - expBefore);
			AssertArtisanFollowUpGates(player, available: true);
			// Ordinary acceptance also proves that all seven masters have registered handlers.
			foreach ((int followUp, int giver) in ArtisanFollowUps)
			{
				Npc npc = await MoveToArtisanAsync(session, player, giver, token);
				await StartQuestAsync(session, npc.GetObjectId(), followUp, token);
				Assert.Equal(QuestStatus.START, player.GetQuestStateList().GetQuestState(followUp).GetStatus());
			}
		}
		foreach (QuestItems item in rewards.GetRewardItem())
			Assert.Equal((long)item.GetCount(), player.GetInventory().GetItemCountByItemId(item.GetItemId()));
		QuestWorkItems? workItems = DataManager.QUEST_DATA.GetQuestById(questId).GetQuestWorkItems();
		foreach (QuestItems item in workItems?.GetQuestWorkItem() ?? [])
			Assert.Equal(0L, player.GetInventory().GetItemCountByItemId(item.GetItemId()));
		policy.AssertClean();
	}

	private static readonly (int Quest, int Giver)[] ArtisanFollowUps =
		[(2905, 204104), (2906, 204106), (2907, 204100), (2908, 204108), (2909, 204102), (2910, 204110), (29049, 798452)];

	private static void AssertArtisanFollowUpGates(Player player, bool available)
	{
		foreach ((int quest, _) in ArtisanFollowUps)
			Assert.Equal(available, QuestService.CheckStartConditions(player, quest, false));
	}

	private async Task<Npc> MoveToArtisanAsync(SimulationL0Session session, Player player, int giver, CancellationToken token)
	{
		Npc npc = FindLivingNpc(player, giver);
		session.Api.World.BeginWorldReload();
		await TeleportForSetupAsync(session, player, npc.GetWorldId(), npc.GetX() - 1, npc.GetY(), npc.GetZ(), token,
			npc.GetInstanceId());
		await MoveBesideAsync(session, npc, token);
		return npc;
	}

	private async Task PlayConstructionBasicsAsync(SimulationL0Session session, Player player, QuestRunPlan plan,
		SimulationQuestRunDriver driver, CancellationToken token)
	{
		Assert.Equal("item_collecting", plan.Template);
		QuestRunStep collection = Assert.Single(plan.Steps, step => step.Kind == "collect");
		Assert.Equal(152025175, collection.ItemId);
		Assert.Equal(3, collection.Count);
		// The plan compiler does not infer recipe sources for item_collecting yet. Use the shipped
		// recipe explicitly here while taking the giver, end NPC and required count from the compiled plan.
		await driver.ExecuteAsync(plan, new(QuestRunOperationKind.StartAtNpc, Npcs: plan.StartNpcs), token);
		var recipe = DataManager.RECIPE_DATA.GetRecipeTemplateById(155008883);
		Assert.Equal(collection.ItemId, recipe.GetProductId());
		Assert.Equal(1, recipe.GetQuantity());
		Assert.Equal(Race.ASMODIANS, recipe.GetRace());
		player.GetSkillList().AddSkill(player, recipe.GetSkillId(), recipe.GetSkillpoint());
		if (!player.GetRecipeList().IsRecipePresent(recipe.GetId()))
			Assert.True(player.GetRecipeList().AddRecipe(player, recipe.GetId()));
		var materials = recipe.GetComponents()[0].GetComponent().GroupBy(c => c.GetItemId())
			.Select(g => (ItemId: g.Key, Count: g.Sum(c => (long)c.GetQuantity()))).ToArray();
		foreach (var material in materials)
			Assert.Equal(0, ItemService.AddItem(player, material.ItemId, material.Count * 4));

		var stations = new List<StaticObject>();
		fixture.World.ForEachObject(o =>
		{
			if (o is StaticObject s && s.IsSpawned() && s.GetWorldId() == 120010000 &&
				s.GetInstanceId() == player.GetInstanceId() && s.GetObjectTemplate().GetTemplateId() == 150000023)
				stations.Add(s);
		});
		StaticObject station = stations.OrderBy(s => s.GetObjectId()).First();
		int failureChance = CraftConfig.MAX_CRAFT_FAILURE_CHANCE;
		CraftConfig.MAX_CRAFT_FAILURE_CHANCE = 0;
		try
		{
			for (int craft = 1; craft <= 4; craft++)
			{
				session.BeginStep($"craft-{craft}", "construction-basics-iron-clamp");
				session.Api.World.BeginWorldReload();
				await TeleportForSetupAsync(session, player, station.GetWorldId(), station.GetX() - 1,
					station.GetY(), station.GetZ(), token, station.GetInstanceId());
				int packetStart = session.PacketHistory.Count;
				await session.SendPacketAsync(session.Api.Craft(150000023, recipe.GetId(), station.GetObjectId(), materials), token);
				await session.SynchronizeAsync(token);
				Assert.NotNull(player.GetInteractionTask());
				long deadline = fixture.Clock.NowMillis + 120_000;
				while (player.GetInteractionTask() != null)
				{
					long next = fixture.Clock.NextDueMillis ?? throw new InvalidDataException("Crafting has no scheduled work.");
					Assert.InRange(next, fixture.Clock.NowMillis, deadline);
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(next - fixture.Clock.NowMillis), token);
				}
				await session.SynchronizeAsync(token);
				var completion = Assert.Single(session.PacketHistory.Skip(packetStart), p =>
					p.PacketType == typeof(SM_CRAFT_UPDATE) && p.Get<byte>("action") >= 4);
				Assert.Equal((byte)5, completion.Get<byte>("action"));
				Assert.Equal(collection.ItemId, completion.Get<int>("itemId"));
				Assert.Equal((long)craft, player.GetInventory().GetItemCountByItemId(collection.ItemId));
				Assert.Equal(craft, ItemCount(session.Api.World, collection.ItemId));
				foreach (var material in materials)
					Assert.Equal(material.Count * (4 - craft), player.GetInventory().GetItemCountByItemId(material.ItemId));
				if (craft == 2)
				{
					Npc darfen = await MoveToArtisanAsync(session, player, Assert.Single(plan.EndNpcs).Id, token);
					await session.SendPacketAsync(session.Api.TalkTo(darfen.GetObjectId()), token);
					await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
					await session.SendPacketAsync(session.Api.SelectDialog(darfen.GetObjectId(),
						DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: plan.Id), token);
					await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
					Assert.Equal(QuestStatus.START, player.GetQuestStateList().GetQuestState(plan.Id).GetStatus());
					Assert.Equal((byte)3, session.Api.World.Quests[plan.Id].Status);
					Assert.Equal(2L, player.GetInventory().GetItemCountByItemId(collection.ItemId));
					await session.SendPacketAsync(session.Api.CloseDialog(darfen.GetObjectId()), token);
				}
			}
		}
		finally
		{
			CraftConfig.MAX_CRAFT_FAILURE_CHANCE = failureChance;
		}
		long expBeforeReward = player.GetCommonData().GetExp();
		await driver.ExecuteAsync(plan, new(QuestRunOperationKind.ClaimReward), token);
		Assert.Equal(DataManager.QUEST_DATA.GetQuestById(plan.Id).GetRewards()[0].GetExp(),
			player.GetCommonData().GetExp() - expBeforeReward);
		Assert.Equal(1L, player.GetInventory().GetItemCountByItemId(collection.ItemId));
		Assert.Equal(1, ItemCount(session.Api.World, collection.ItemId));
		Assert.Empty(fixture.Clock.Faults);
	}
}
